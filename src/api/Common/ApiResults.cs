namespace MountainManager.Api.Common;

public static class ApiResults
{
    public static IResult Ok<T>(T data, string traceId) =>
        Results.Json(ApiResponse<T>.Ok(data, traceId), statusCode: StatusCodes.Status200OK);

    public static IResult Created<T>(string location, T data, string traceId) =>
        Results.Created(location, ApiResponse<T>.Ok(data, traceId));

    public static IResult EmptyOk(string traceId) =>
        Results.Json(ApiResponse<object>.Ok(new { }, traceId), statusCode: StatusCodes.Status200OK);

    public static IResult Error(int statusCode, ApiError error, string traceId) =>
        Results.Json(ApiResponse<object>.Fail(error, traceId), statusCode: statusCode);

    public static Task WriteErrorAsync(HttpContext httpContext, int statusCode, ApiError error)
    {
        httpContext.Response.StatusCode = statusCode;
        return httpContext.Response.WriteAsJsonAsync(ApiResponse<object>.Fail(error, httpContext.TraceIdentifier));
    }
}
