using System.Net;
using System.Net.Http.Json;
using HAQPOS.Application.Areas;
using HAQPOS.Application.Cash;
using HAQPOS.Application.Clients;
using HAQPOS.Application.Inventory;
using HAQPOS.Application.Menu;
using HAQPOS.Application.Users;
using HAQPOS.Domain.Users;
using HAQPOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HAQPOS.IntegrationTests;

public sealed partial class UserManagementTests
{
    private sealed record CashSetup(HttpClient Admin, HttpClient Cashier, UserView User,
        CashShiftView Shift, Guid OtherAreaId, InventoryItemView Item, InventoryItemView OtherItem,
        MenuProductView Menu, ClientView Client);

    private static async Task<T> CashOk<T>(HttpResponseMessage response)
    {
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }

    private async Task<CashSetup> PrepareCashAsync()
    {
        var admin = NewClient();
        await Login(admin, AdminEmployee, AdminPassword);
        var area = await CashOk<AreaView>(await Send(admin, "/api/areas/", new CreateAreaRequest("Club Médico", "Área de venta")));
        var other = await CashOk<AreaView>(await Send(admin, "/api/areas/", new CreateAreaRequest("Cafetería", "Otra área")));
        var item = await CashOk<InventoryItemView>(await Send(admin, $"/api/inventory/areas/{area.Id}/items",
            new CreateInventoryItemRequest("CASH-AGUA", "Agua del club", "Pieza", 1, 25, 5)));
        var otherItem = await CashOk<InventoryItemView>(await Send(admin, $"/api/inventory/areas/{other.Id}/items",
            new CreateInventoryItemRequest("CASH-AGUA", "Agua de otra área", "Pieza", 1, 99, 30)));
        var menu = await CashOk<MenuProductView>(await Send(admin, $"/api/menu/areas/{area.Id}/products", new CreateMenuProductRequest("Comida del día", "Comida", null, 50)));
        var (cashier, user) = await NewCashierAsync(admin, area.Id, "77101");
        var shift = await CashOk<CashShiftView>(await Send(cashier, "/api/cash/shifts", new OpenCashShiftRequest(Guid.NewGuid(), area.Id, 100)));
        var customer = await CashOk<ClientView>(await Send(cashier, "/api/cash/clients", new CreateCashClientRequest("GASS01", "Doctora de Prueba", true, null)));
        return new(admin, cashier, user, shift, other.Id, item, otherItem, menu, customer);
    }

    private async Task<(HttpClient Client, UserView User)> NewCashierAsync(HttpClient admin, Guid areaId, string employee)
    {
        var credentials = await CashOk<TemporaryCredentials>(await Send(admin, "/api/users", new CreateUserRequest(employee, "Cajero de Prueba", UserRole.Cajero, areaId)));
        var client = NewClient();
        await Login(client, employee, credentials.TemporaryPassword);
        var changed = await CashOk<SessionResponse>(await Send(client, "/api/auth/change-password", new ChangePasswordRequest(credentials.TemporaryPassword, PersonalPassword, PersonalPassword)));
        return (client, changed.User!);
    }

    private static async Task<CashAccountDetail> NewCashAccount(CashSetup setup) =>
        await CashOk<CashAccountDetail>(await Send(setup.Cashier, $"/api/cash/shifts/{setup.Shift.Id}/accounts", new OpenCashAccountRequest(Guid.NewGuid(), setup.Client.Id)));

    private async Task<decimal> CashStock(Guid id)
    {
        await using var scope = factory!.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<HaqposDbContext>().InventoryItems.Where(i => i.Id == id).Select(i => i.CurrentStock).SingleAsync();
    }

    [PostgresFact]
    public async Task CashEnforcesAssignedAreaOwnershipAndNormalClientRegistration()
    {
        var s = await PrepareCashAsync();
        using var admin = s.Admin; using var cashier = s.Cashier;
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(cashier, "/api/cash/shifts", new OpenCashShiftRequest(Guid.NewGuid(), s.OtherAreaId, 0))).StatusCode);
        var products = await cashier.GetFromJsonAsync<List<CashProductView>>($"/api/cash/shifts/{s.Shift.Id}/products?search=Agua", JsonOptions);
        Assert.Contains(products!, p => p.Id == s.Item.Id);
        Assert.DoesNotContain(products!, p => p.Id == s.OtherItem.Id);
        var account = await NewCashAccount(s);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(cashier, $"/api/cash/accounts/{account.Account.Id}/lines",
            new AddCashLineRequest(Guid.NewGuid(), "Inventario", s.OtherItem.Id, 1))).StatusCode);
        var (other, _) = await NewCashierAsync(admin, s.Shift.AreaId, "77102");
        using (other)
        {
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/cash/accounts/{account.Account.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Send(other, $"/api/cash/accounts/{account.Account.Id}/lines", new AddCashLineRequest(Guid.NewGuid(), "Inventario", s.Item.Id, 1))).StatusCode);
        }
        var customer = await CashOk<ClientView>(await Send(cashier, "/api/cash/clients", new { registrationNumber = "gass02", fullName = "Nuevo Cliente", isExternal = false, office = "Consultorio de ochenta caracteres permitido", category = "CORTESIA" }));
        Assert.Equal("NORMAL", customer.Category);
        Assert.Equal("GASS02", customer.RegistrationNumber);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(cashier, "/api/cash/clients", new CreateCashClientRequest("GASS02", "Cliente duplicado", true, null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(cashier, "/api/cash/clients", new CreateCashClientRequest("GASS03", "Cliente 123", true, null))).StatusCode);
        var users = await admin.GetFromJsonAsync<UserPage>("/api/users", JsonOptions);
        var current = users!.Items.Single(u => u.Id == s.User.Id);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(admin, $"/api/users/{current.Id}/area", new AssignCashierAreaRequest(s.OtherAreaId, current.Version))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(admin, $"/api/users/{current.Id}", new UpdateUserRequest(current.FullName, UserRole.Supervisor, true, null, current.Version), HttpMethod.Put)).StatusCode);
        Assert.Equal(5, await CashStock(s.Item.Id));
    }

    [PostgresFact]
    public async Task CashStockIsConsumedOnceMenuIsNotConsumedAndCutMatchesPayment()
    {
        var s = await PrepareCashAsync(); using var admin = s.Admin; using var cashier = s.Cashier;
        var account = await NewCashAccount(s);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(cashier, $"/api/cash/shifts/{s.Shift.Id}/close")).StatusCode);
        var request = new AddCashLineRequest(Guid.NewGuid(), "Inventario", s.Item.Id, 3);
        var detail = await CashOk<CashAccountDetail>(await Send(cashier, $"/api/cash/accounts/{account.Account.Id}/lines", request));
        var repeated = await CashOk<CashAccountDetail>(await Send(cashier, $"/api/cash/accounts/{account.Account.Id}/lines", request));
        Assert.Equal(75, repeated.Account.Total); Assert.Single(repeated.Lines); Assert.Equal(2, await CashStock(s.Item.Id));
        Assert.Equal(HttpStatusCode.Conflict, (await Send(cashier, $"/api/cash/accounts/{account.Account.Id}/lines", request with { Quantity = 2 })).StatusCode);
        detail = await CashOk<CashAccountDetail>(await Send(cashier, $"/api/cash/accounts/{account.Account.Id}/lines", new AddCashLineRequest(Guid.NewGuid(), "Menu", s.Menu.Id, 2)));
        Assert.Equal(175, detail.Account.Total); Assert.Equal(2, await CashStock(s.Item.Id));
        Assert.Equal(HttpStatusCode.Conflict, (await Send(cashier, $"/api/cash/accounts/{account.Account.Id}/pay", new PayCashAccountRequest("EFECTIVO", account.Account.Version))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(cashier, $"/api/cash/accounts/{account.Account.Id}/pay",
            new PayCashAccountRequest("EFECTIVO", detail.Account.Version, AmountReceived: 174))).StatusCode);
        var pay = new PayCashAccountRequest("EFECTIVO", detail.Account.Version, AmountReceived: 200);
        detail = await CashOk<CashAccountDetail>(await Send(cashier, $"/api/cash/accounts/{account.Account.Id}/pay", pay));
        await CashOk<CashAccountDetail>(await Send(cashier, $"/api/cash/accounts/{account.Account.Id}/pay", pay));
        Assert.Equal("Pagada", detail.Account.Status); Assert.Equal(200, detail.AmountReceived); Assert.Equal(25, detail.ChangeGiven); Assert.Equal(2, await CashStock(s.Item.Id));
        Assert.Equal(HttpStatusCode.Conflict, (await Send(cashier, $"/api/cash/accounts/{account.Account.Id}/lines", new AddCashLineRequest(Guid.NewGuid(), "Menu", s.Menu.Id, 1))).StatusCode);
        var closed = await CashOk<CashShiftView>(await Send(cashier, $"/api/cash/shifts/{s.Shift.Id}/close"));
        Assert.Equal(175, closed.Total); Assert.Equal(175, closed.CashTotal); Assert.Equal(275, closed.ExpectedCash); Assert.Equal(1, closed.PaidAccounts);
        Assert.Equal(200, closed.CashReceivedTotal); Assert.Equal(25, closed.ChangeGivenTotal);
        var cut = await cashier.GetFromJsonAsync<CashCut>($"/api/cash/shifts/{s.Shift.Id}/cut", JsonOptions);
        Assert.Equal(2, Assert.Single(cut!.Accounts).Lines.Count);
        var receipt = await cashier.GetAsync($"/api/cash/shifts/{s.Shift.Id}/receipt?download=true");
        Assert.Equal(HttpStatusCode.OK, receipt.StatusCode); Assert.Equal("application/pdf", receipt.Content.Headers.ContentType!.MediaType);
        Assert.StartsWith("%PDF-1.4", await receipt.Content.ReadAsStringAsync());
        Assert.Contains("attachment", receipt.Content.Headers.ContentDisposition!.ToString());
        Assert.Equal(HttpStatusCode.Conflict, (await Send(cashier, $"/api/cash/shifts/{s.Shift.Id}/accounts", new OpenCashAccountRequest(Guid.NewGuid(), s.Client.Id))).StatusCode);
    }

    [PostgresFact]
    public async Task CashRemovalAndCancellationRestoreStockExactlyOnce()
    {
        var s = await PrepareCashAsync(); using var admin = s.Admin; using var cashier = s.Cashier;
        var first = await NewCashAccount(s); var second = await NewCashAccount(s);
        var detail = await CashOk<CashAccountDetail>(await Send(cashier, $"/api/cash/accounts/{first.Account.Id}/lines", new AddCashLineRequest(Guid.NewGuid(), "Inventario", s.Item.Id, 2)));
        var lineId = Assert.Single(detail.Lines).Id;
        var removePath = $"/api/cash/accounts/{first.Account.Id}/lines/{lineId}/remove";
        await CashOk<CashAccountDetail>(await Send(cashier, removePath, new RemoveCashLineRequest("Producto equivocado")));
        await CashOk<CashAccountDetail>(await Send(cashier, removePath, new RemoveCashLineRequest("Producto equivocado")));
        Assert.Equal(5, await CashStock(s.Item.Id));
        detail = await CashOk<CashAccountDetail>(await Send(cashier, $"/api/cash/accounts/{first.Account.Id}/lines", new AddCashLineRequest(Guid.NewGuid(), "Inventario", s.Item.Id, 3)));
        Assert.Equal(HttpStatusCode.Conflict, (await Send(cashier, $"/api/cash/accounts/{second.Account.Id}/lines", new AddCashLineRequest(Guid.NewGuid(), "Inventario", s.Item.Id, 3))).StatusCode);
        Assert.Equal(2, await CashStock(s.Item.Id));
        var cancel = new CancelCashAccountRequest("El cliente canceló", detail.Account.Version);
        await CashOk<CashAccountDetail>(await Send(cashier, $"/api/cash/accounts/{first.Account.Id}/cancel", cancel));
        await CashOk<CashAccountDetail>(await Send(cashier, $"/api/cash/accounts/{first.Account.Id}/cancel", cancel));
        Assert.Equal(5, await CashStock(s.Item.Id));
        await CashOk<CashAccountDetail>(await Send(cashier, $"/api/cash/accounts/{second.Account.Id}/cancel", new CancelCashAccountRequest("Cuenta sin consumo", second.Account.Version)));
        var closed = await CashOk<CashShiftView>(await Send(cashier, $"/api/cash/shifts/{s.Shift.Id}/close"));
        Assert.Equal(0, closed.Total); Assert.Equal(2, closed.CancelledAccounts);
    }

    [PostgresFact]
    public async Task CashCourtesyAccountsStayZeroKeepConsumedItemsAndCannotBeCharged()
    {
        var s = await PrepareCashAsync(); using var admin = s.Admin; using var cashier = s.Cashier;
        var changed = await CashOk<ClientView>(await Send(admin, $"/api/clients/{s.Client.Id}",
            new UpdateClientRequest(s.Client.RegistrationNumber, s.Client.FullName, "CORTESIA", true, null, s.Client.Version), HttpMethod.Put));
        var account = await CashOk<CashAccountDetail>(await Send(cashier, $"/api/cash/shifts/{s.Shift.Id}/accounts",
            new OpenCashAccountRequest(Guid.NewGuid(), changed.Id)));
        var detail = await CashOk<CashAccountDetail>(await Send(cashier, $"/api/cash/accounts/{account.Account.Id}/lines",
            new AddCashLineRequest(Guid.NewGuid(), "Inventario", s.Item.Id, 2)));
        Assert.Equal(0, detail.Account.Total);
        Assert.Equal(50, Assert.Single(detail.Lines).Total);
        Assert.Equal(3, await CashStock(s.Item.Id));
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(cashier, $"/api/cash/accounts/{account.Account.Id}/pay",
            new PayCashAccountRequest("EFECTIVO", detail.Account.Version, AmountReceived: 0))).StatusCode);
        detail = await CashOk<CashAccountDetail>(await Send(cashier, $"/api/cash/accounts/{account.Account.Id}/pay",
            new PayCashAccountRequest("CORTESIA", detail.Account.Version)));
        Assert.Equal("Pagada", detail.Account.Status);
        Assert.Equal("CORTESIA", detail.Account.PaymentMethod);
        Assert.Equal(0, detail.Account.Total);
        Assert.Equal(3, await CashStock(s.Item.Id));
        var closed = await CashOk<CashShiftView>(await Send(cashier, $"/api/cash/shifts/{s.Shift.Id}/close"));
        Assert.Equal(0, closed.Total); Assert.Equal(1, closed.CourtesyAccounts); Assert.Equal(1, closed.PaidAccounts);
        Assert.Equal(0, closed.ExpectedCash - s.Shift.OpeningAmount);
        var receipt = await cashier.GetAsync($"/api/cash/shifts/{s.Shift.Id}/receipt?download=true");
        var pdf = await receipt.Content.ReadAsStringAsync();
        Assert.Contains("CORTESIA", pdf); Assert.Contains("importe a pagar $0.00", pdf);
    }

    [PostgresFact]
    public async Task CashConcurrentCashiersCannotOversellTheSameAreaInventory()
    {
        var s = await PrepareCashAsync(); using var admin = s.Admin; using var first = s.Cashier;
        var (second, _) = await NewCashierAsync(admin, s.Shift.AreaId, "77102"); using var secondClient = second;
        var secondShift = await CashOk<CashShiftView>(await Send(second, "/api/cash/shifts", new OpenCashShiftRequest(Guid.NewGuid(), s.Shift.AreaId, 0)));
        var a = await NewCashAccount(s);
        var b = await CashOk<CashAccountDetail>(await Send(second, $"/api/cash/shifts/{secondShift.Id}/accounts", new OpenCashAccountRequest(Guid.NewGuid(), s.Client.Id)));
        var results = await Task.WhenAll(
            Send(first, $"/api/cash/accounts/{a.Account.Id}/lines", new AddCashLineRequest(Guid.NewGuid(), "Inventario", s.Item.Id, 4)),
            Send(second, $"/api/cash/accounts/{b.Account.Id}/lines", new AddCashLineRequest(Guid.NewGuid(), "Inventario", s.Item.Id, 4)));
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(1, await CashStock(s.Item.Id));
        var workspace = await first.GetFromJsonAsync<CashWorkspace>("/api/cash/workspace", JsonOptions);
        Assert.Single(workspace!.Accounts); Assert.Equal(s.Shift.Id, workspace.Shift!.Id);
    }

    [PostgresFact]
    public async Task CashPreservesCustomerAndPriceSnapshotsAndSeparatesPaymentMethods()
    {
        var s = await PrepareCashAsync(); using var admin = s.Admin; using var cashier = s.Cashier;
        foreach (var method in new[] { "EFECTIVO", "CXC", "TARJETA" })
        {
            var account = await NewCashAccount(s);
            var detail = await CashOk<CashAccountDetail>(await Send(cashier, $"/api/cash/accounts/{account.Account.Id}/lines", new AddCashLineRequest(Guid.NewGuid(), "Menu", s.Menu.Id, 1)));
            Assert.Equal(HttpStatusCode.BadRequest, (await Send(cashier, $"/api/cash/accounts/{account.Account.Id}/pay", new PayCashAccountRequest("OTRO", detail.Account.Version))).StatusCode);
            var payment = method switch
            {
                "EFECTIVO" => new PayCashAccountRequest(method, detail.Account.Version, AmountReceived: 50),
                "CXC" => new PayCashAccountRequest(method, detail.Account.Version),
                _ => new PayCashAccountRequest(method, detail.Account.Version, CardLastFour: "1234", CardAuthorization: "AUTH-123", CardBank: "Banco de Prueba", CardType: "DEBITO")
            };
            if (method == "TARJETA")
                Assert.Equal(HttpStatusCode.BadRequest, (await Send(cashier, $"/api/cash/accounts/{account.Account.Id}/pay",
                    new PayCashAccountRequest("TARJETA", detail.Account.Version))).StatusCode);
            detail = await CashOk<CashAccountDetail>(await Send(cashier, $"/api/cash/accounts/{account.Account.Id}/pay", payment));
            if (method == "CXC") Assert.Equal("Pendiente", detail.Account.Status);
            if (method == "TARJETA") Assert.Equal("1234", detail.CardLastFour);
        }
        await CashOk<MenuProductView>(await Send(admin, $"/api/menu/areas/{s.Shift.AreaId}/products/{s.Menu.Id}",
            new UpdateMenuProductRequest("Nombre posterior del menú", "Comida", null, 150, s.Menu.Version), HttpMethod.Put));
        await CashOk<ClientView>(await Send(admin, $"/api/clients/{s.Client.Id}", new UpdateClientRequest("CAMBIO", "Nombre posterior", "CONVENIO", true, null, s.Client.Version), HttpMethod.Put));
        var clientPage = await admin.GetFromJsonAsync<ClientPage>("/api/clients?search=CAMBIO", JsonOptions);
        Assert.Equal(HttpStatusCode.NoContent, (await Send(admin, $"/api/clients/{s.Client.Id}/delete", new DeleteClientRequest(clientPage!.Items.Single().Version))).StatusCode);
        var closed = await CashOk<CashShiftView>(await Send(cashier, $"/api/cash/shifts/{s.Shift.Id}/close"));
        Assert.Equal(50, closed.CashTotal); Assert.Equal(50, closed.CxcTotal); Assert.Equal(50, closed.CardTotal); Assert.Equal(150, closed.Total);
        Assert.Equal(1, closed.PendingAccounts);
        var receivables = await cashier.GetFromJsonAsync<CashReceivablesPage>("/api/cash/receivables", JsonOptions);
        Assert.Equal(1, receivables!.Total); Assert.Equal(50, receivables.TotalAmount);
        Assert.Equal("Doctora de Prueba", Assert.Single(receivables.Items).Account.ClientName);
        var adminReceivables = await admin.GetFromJsonAsync<CashReceivablesPage>("/api/cash/receivables", JsonOptions);
        Assert.Equal(1, adminReceivables!.Total);
        Assert.Equal(2, closed.PaidAccounts);
        var cut = await cashier.GetFromJsonAsync<CashCut>($"/api/cash/shifts/{s.Shift.Id}/cut", JsonOptions);
        Assert.All(cut!.Accounts, a => { Assert.Equal("Doctora de Prueba", a.Account.ClientName); Assert.Equal("GASS01", a.Account.RegistrationNumber); Assert.Equal("NORMAL", a.Category); Assert.Equal(50, a.Lines.Single().UnitPrice); });
        Assert.Contains(cut.Accounts, a => a.Account.Status == "Pendiente" && a.Account.PaymentMethod == "CXC");
        var consultant = await Create(admin, "77103", UserRole.Consultor);
        using var reader = NewClient(); await Login(reader, "77103", consultant.TemporaryPassword);
        await CashOk<SessionResponse>(await Send(reader, "/api/auth/change-password", new ChangePasswordRequest(consultant.TemporaryPassword, PersonalPassword, PersonalPassword)));
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(reader, "/api/cash/shifts", new OpenCashShiftRequest(Guid.NewGuid(), s.Shift.AreaId, 0))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await reader.GetAsync($"/api/cash/shifts/{s.Shift.Id}/cut")).StatusCode);
    }
}
