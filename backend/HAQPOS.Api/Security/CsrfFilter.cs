using Microsoft.AspNetCore.Antiforgery;

namespace HAQPOS.Api.Security;

internal sealed class CsrfFilter(IAntiforgery antiforgery) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (!HttpMethods.IsGet(context.HttpContext.Request.Method) &&
            !HttpMethods.IsHead(context.HttpContext.Request.Method) &&
            !HttpMethods.IsOptions(context.HttpContext.Request.Method))
        {
            try { await antiforgery.ValidateRequestAsync(context.HttpContext); }
            catch (AntiforgeryValidationException)
            {
                return Results.Problem(statusCode: 400, title: "La solicitud de seguridad caducó. Actualiza la página e inténtalo nuevamente.");
            }
        }
        return await next(context);
    }
}
