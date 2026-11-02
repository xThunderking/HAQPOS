using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using HAQPOS.Application.Users;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HAQPOS.Api.Errors;

internal sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is UserOperationException userError)
        {
            httpContext.Response.StatusCode = userError.Error switch
            {
                UserError.InvalidCredentials => 401,
                UserError.Locked => 423,
                UserError.Forbidden => 403,
                UserError.Conflict => 409,
                UserError.NotFound => 404,
                _ => 400
            };
            return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = new ProblemDetails { Status = httpContext.Response.StatusCode, Title = userError.Message }
            });
        }
        if (exception is DbUpdateConcurrencyException ||
            exception is DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } })
        {
            httpContext.Response.StatusCode = 409;
            return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = new ProblemDetails { Status = 409, Title = "Los datos cambiaron o el número de empleado ya existe. Actualiza la página." }
            });
        }
        logger.LogError(exception, "Unhandled error. TraceId: {TraceId}", httpContext.TraceIdentifier);
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Ocurrió un error interno.",
                Extensions = { ["traceId"] = httpContext.TraceIdentifier }
            }
        });
    }
}
