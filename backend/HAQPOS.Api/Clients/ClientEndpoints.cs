using HAQPOS.Api.Security;
using HAQPOS.Application.Clients;
using HAQPOS.Infrastructure.Clients;

namespace HAQPOS.Api.Clients;

internal static class ClientEndpoints
{
    public static void MapClientEndpoints(this WebApplication app)
    {
        var clients = app.MapGroup("/api/clients")
            .AddEndpointFilter<CsrfFilter>();

        clients.MapGet("/", async (string? search, int? page, ClientService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(search, page ?? 1, ct)))
            .RequireAuthorization(UserAuthentication.ModuleViewPolicy);

        clients.MapPost("/", async (CreateClientRequest request, HttpContext context,
            ClientService service, CancellationToken ct) =>
            Results.Ok(await service.CreateAsync(request, context.User.UserId(),
                context.Connection.RemoteIpAddress?.ToString(), ct)))
            .RequireAuthorization(UserAuthentication.ClientPolicy);

        clients.MapPut("/{id:guid}", async (Guid id, UpdateClientRequest request, HttpContext context,
            ClientService service, CancellationToken ct) =>
            Results.Ok(await service.UpdateAsync(id, request, context.User.UserId(),
                context.Connection.RemoteIpAddress?.ToString(), ct)))
            .RequireAuthorization(UserAuthentication.ClientPolicy);

        clients.MapPost("/{id:guid}/delete", async (Guid id, DeleteClientRequest request, HttpContext context,
            ClientService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(id, request, context.User.UserId(),
                context.Connection.RemoteIpAddress?.ToString(), ct);
            return Results.NoContent();
        }).RequireAuthorization(UserAuthentication.ClientPolicy);
    }
}
