using HAQPOS.Api.Security;
using HAQPOS.Application.Cash;
using HAQPOS.Infrastructure.Cash;

namespace HAQPOS.Api.Cash;

internal static class CashEndpoints
{
    public static void MapCashEndpoints(this WebApplication app)
    {
        var cash = app.MapGroup("/api/cash").AddEndpointFilter<CsrfFilter>()
            .RequireAuthorization(UserAuthentication.ModuleViewPolicy);
        cash.MapGet("/workspace", async (HttpContext context, CashService service, CancellationToken ct) =>
            Results.Ok(await service.WorkspaceAsync(context.User.UserId(), ct)));
        cash.MapGet("/shifts", async (int? page, HttpContext context, CashService service, CancellationToken ct) =>
            Results.Ok(await service.HistoryAsync(context.User.UserId(), page ?? 1, ct)));
        cash.MapGet("/receivables", async (int? page, string? search, HttpContext context, CashService service, CancellationToken ct) =>
            Results.Ok(await service.ReceivablesAsync(context.User.UserId(), page ?? 1, search, ct)));
        cash.MapPost("/shifts", async (OpenCashShiftRequest request, HttpContext context, CashService service, CancellationToken ct) =>
            Results.Ok(await service.OpenShiftAsync(request, context.User.UserId(), Ip(context), ct)));
        cash.MapGet("/shifts/{id:guid}/products", async (Guid id, string? search, HttpContext context, CashService service, CancellationToken ct) =>
            Results.Ok(await service.ProductsAsync(id, search, context.User.UserId(), ct)));
        cash.MapPost("/shifts/{id:guid}/accounts", async (Guid id, OpenCashAccountRequest request, HttpContext context, CashService service, CancellationToken ct) =>
            Results.Ok(await service.OpenAccountAsync(id, request, context.User.UserId(), Ip(context), ct)));
        cash.MapPost("/shifts/{id:guid}/close", async (Guid id, HttpContext context, CashService service, CancellationToken ct) =>
            Results.Ok(await service.CloseShiftAsync(id, context.User.UserId(), Ip(context), ct)));
        cash.MapGet("/shifts/{id:guid}/cut", async (Guid id, HttpContext context, CashService service, CancellationToken ct) =>
            Results.Ok(await service.CutAsync(id, context.User.UserId(), ct)));
        cash.MapGet("/shifts/{id:guid}/receipt", async (Guid id, bool? download, HttpContext context, CashService service, CancellationToken ct) =>
        {
            var cut = await service.CutAsync(id, context.User.UserId(), ct);
            return Results.File(CashReceiptPdf.Create(cut), "application/pdf",
                fileDownloadName: download == true ? $"HAQPOS-corte-{cut.Shift.Number:D6}.pdf" : null);
        });
        cash.MapPost("/clients", async (CreateCashClientRequest request, HttpContext context, CashService service, CancellationToken ct) =>
            Results.Ok(await service.CreateClientAsync(request, context.User.UserId(), Ip(context), ct)));
        cash.MapGet("/accounts/{id:guid}", async (Guid id, HttpContext context, CashService service, CancellationToken ct) =>
            Results.Ok(await service.AccountAsync(id, context.User.UserId(), ct)));
        cash.MapPost("/accounts/{id:guid}/lines", async (Guid id, AddCashLineRequest request, HttpContext context, CashService service, CancellationToken ct) =>
            Results.Ok(await service.AddLineAsync(id, request, context.User.UserId(), Ip(context), ct)));
        cash.MapPost("/accounts/{id:guid}/lines/{lineId:guid}/remove", async (Guid id, Guid lineId, RemoveCashLineRequest request, HttpContext context, CashService service, CancellationToken ct) =>
            Results.Ok(await service.RemoveLineAsync(id, lineId, request, context.User.UserId(), Ip(context), ct)));
        cash.MapPost("/accounts/{id:guid}/pay", async (Guid id, PayCashAccountRequest request, HttpContext context, CashService service, CancellationToken ct) =>
            Results.Ok(await service.PayAsync(id, request, context.User.UserId(), Ip(context), ct)));
        cash.MapPost("/accounts/{id:guid}/cancel", async (Guid id, CancelCashAccountRequest request, HttpContext context, CashService service, CancellationToken ct) =>
            Results.Ok(await service.CancelAsync(id, request, context.User.UserId(), Ip(context), ct)));
    }
    private static string? Ip(HttpContext context) => context.Connection.RemoteIpAddress?.ToString();
}
