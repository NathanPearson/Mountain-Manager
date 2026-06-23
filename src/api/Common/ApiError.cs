namespace MountainManager.Api.Common;

public sealed record ApiError(
    string Code,
    string Message,
    IReadOnlyDictionary<string, string[]>? Details = null)
{
    public static ApiError Validation(IReadOnlyDictionary<string, string[]> details) =>
        new("VALIDATION_ERROR", "One or more validation errors occurred.", details);

    public static ApiError Unauthorized(string message) =>
        new("UNAUTHORIZED", message);

    public static ApiError Forbidden(string message) =>
        new("FORBIDDEN", message);

    public static ApiError NotFound(string message) =>
        new("NOT_FOUND", message);

    public static ApiError Conflict(string message) =>
        new("CONFLICT", message);

    public static ApiError Unexpected() =>
        new("UNEXPECTED_ERROR", "An unexpected error occurred.");
}
