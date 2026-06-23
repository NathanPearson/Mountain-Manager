using NodaTime;

namespace MountainManager.Api.Tasks;

public sealed record TaskResponse(
    Guid Id,
    string Title,
    string? Description,
    string Priority,
    LocalDate DueDate,
    string DueBucket,
    bool IsCompleted,
    Instant CreatedAt,
    Instant UpdatedAt,
    Instant? CompletedAt);

public sealed record TaskCreateRequest(
    string Title,
    string? Description,
    string? Priority,
    LocalDate? DueDate);

public sealed record TaskUpdateRequest(
    string Title,
    string? Description,
    string Priority,
    LocalDate? DueDate);

public sealed record TaskCompletionRequest(bool IsCompleted);
