using NodaTime;

namespace MountainManager.Api.Data;

public sealed class TaskItem
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int PriorityId { get; set; } = TaskPriority.MediumId;
    public TaskPriority Priority { get; set; } = null!;
    public LocalDate DueDate { get; set; }
    public bool IsCompleted { get; set; }
    public Instant CreatedAt { get; set; }
    public Instant UpdatedAt { get; set; }
    public Instant? CompletedAt { get; set; }
}
