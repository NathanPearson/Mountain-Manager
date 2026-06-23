using MountainManager.Api.Data;
using NodaTime;

namespace MountainManager.Api.Tasks;

public static class TaskMapping
{
    public static TaskResponse ToResponse(this TaskItem task, LocalDate today)
    {
        return new TaskResponse(
            task.Id,
            task.Title,
            task.Description,
            task.Priority.Name,
            task.DueDate,
            GetDueBucket(task, today),
            task.IsCompleted,
            task.CreatedAt,
            task.UpdatedAt,
            task.CompletedAt);
    }

    private static string GetDueBucket(TaskItem task, LocalDate today)
    {
        if (task.IsCompleted)
        {
            return "Completed";
        }

        if (task.DueDate < today)
        {
            return "Overdue";
        }

        if (task.DueDate == today)
        {
            return "Today";
        }

        return "Upcoming";
    }
}
