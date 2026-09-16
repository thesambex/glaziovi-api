using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace Glaziovi.Web.Middlewares;

public sealed class GlobalExceptionHandlerMiddleware(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandlerMiddleware> logger
) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        var statusCode = StatusCodes.Status500InternalServerError;
        var detail = "A server error occurred";

        if (exception is BadHttpRequestException badRequest)
        {
            statusCode = badRequest.StatusCode;
            detail = "The request is invalid";
            logger.LogWarning(exception, "Invalid request: {msg}", exception.Message);
        }
        else
        {
            logger.LogCritical(exception, "Internal Server Error: {msg}", exception.Message);
        }

        httpContext.Response.StatusCode = statusCode;

        var problem = new ProblemDetails
        {
            Title = ReasonPhrases.GetReasonPhrase(statusCode),
            Status = statusCode,
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext, ProblemDetails = problem
        });

        return true;
    }
}
