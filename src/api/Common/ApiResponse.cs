namespace MountainManager.Api.Common;

public sealed record ApiResponse<T>(
    bool Success,
    T? Data,
    ApiError? Error,
    string TraceId)
{
    public static ApiResponse<T> Ok(T data, string traceId) =>
        new(true, data, null, traceId);

    public static ApiResponse<T> Fail(ApiError error, string traceId) =>
        new(false, default, error, traceId);
}
