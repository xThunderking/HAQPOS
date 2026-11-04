using HAQPOS.Api.Security;
using HAQPOS.Application.Areas;
using HAQPOS.Infrastructure.Areas;

namespace HAQPOS.Api.Areas;

internal static class AreaEndpoints
{
    public static void MapAreaEndpoints(this WebApplication app)
    {
        var areas = app.MapGroup("/api/areas")
            .AddEndpointFilter<CsrfFilter>();

        areas.MapGet("/", async (string? search, int? page, AreaService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(search, page ?? 1, ct)))
            .RequireAuthorization(UserAuthentication.ModuleViewPolicy);

        areas.MapGet("/options", async (AreaService service, CancellationToken ct) =>
            Results.Ok(await service.OptionsAsync(ct)))
            .RequireAuthorization(UserAuthentication.ModuleViewPolicy);

        areas.MapPost("/", async (CreateAreaRequest request, HttpContext context,
            AreaService service, CancellationToken ct) =>
            Results.Ok(await service.CreateAsync(request, context.User.UserId(),
                context.Connection.RemoteIpAddress?.ToString(), ct)))
            .RequireAuthorization(UserAuthentication.AreaPolicy);
    }
}
