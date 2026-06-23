using NodaTime;

namespace MountainManager.Api.Tasks;

public static class TaskValidation
{
    public static Dictionary<string, string[]> ValidateCreate(TaskCreateRequest request)
    {
        var errors = ValidateCommon(request.Title, request.Description, request.Priority, request.DueDate, isPriorityRequired: false);
        return errors;
    }

    public static Dictionary<string, string[]> ValidateUpdate(TaskUpdateRequest request)
    {
        return ValidateCommon(request.Title, request.Description, request.Priority, request.DueDate, isPriorityRequired: true);
    }

    private static Dictionary<string, string[]> ValidateCommon(
        string title,
        string? description,
        string? priority,
        LocalDate? dueDate,
        bool isPriorityRequired)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(title))
        {
            errors["title"] = ["Title is required."];
        }
        else if (title.Trim().Length > 120)
        {
            errors["title"] = ["Title must be 120 characters or fewer."];
        }

        if (description?.Length > 2000)
        {
            errors["description"] = ["Description must be 2,000 characters or fewer."];
        }

        if (isPriorityRequired && string.IsNullOrWhiteSpace(priority))
        {
            errors["priority"] = ["Priority must be Low, Medium, High, or Urgent."];
        }

        if (dueDate is null)
        {
            errors["dueDate"] = ["Due date is required."];
        }

        return errors;
    }
}
