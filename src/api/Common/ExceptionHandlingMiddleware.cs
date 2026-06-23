namespace MountainManager.Api.Common;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (BadHttpRequestException ex)
        {
            logger.LogWarning(
                ex,
                "Bad request while processing {Method} {Path}. TraceId: {TraceId}",
                context.Request.Method,
                context.Request.Path,
                context.TraceIdentifier);

            await ApiResults.WriteErrorAsync(
                context,
                ex.StatusCode,
                ApiError.Validation(new Dictionary<string, string[]>
                {
                    ["request"] = ["The request body or route values could not be processed."]
                }));
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Unhandled exception while processing {Method} {Path}. TraceId: {TraceId}",
                context.Request.Method,
                context.Request.Path,
                context.TraceIdentifier);

            await ApiResults.WriteErrorAsync(
                context,
                StatusCodes.Status500InternalServerError,
                ApiError.Unexpected());
        }
    }
}
