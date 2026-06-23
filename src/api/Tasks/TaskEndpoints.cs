using Microsoft.EntityFrameworkCore;
using MountainManager.Api.Common;
using MountainManager.Api.Data;
using NodaTime;

namespace MountainManager.Api.Tasks;

public static class TaskEndpoints
{
    public static RouteGroupBuilder MapTaskEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/tasks")
            .RequireAuthorization()
            .WithTags("Tasks");

        group.MapGet("/", ListAsync);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapPost("/", CreateAsync);
        group.MapPut("/{id:guid}", UpdateAsync);
        group.MapPatch("/{id:guid}/completion", CompleteAsync);
        group.MapDelete("/{id:guid}", DeleteAsync);

        return group;
    }

    private static async Task<IResult> ListAsync(
        string? priority,
        string? status,
        string? dueBucket,
        AppDbContext db,
        CurrentUser currentUser,
        IClock clock,
        HttpContext httpContext)
    {
        var today = GetToday(clock);
        var query = db.Tasks
            .AsNoTracking()
            .Include(task => task.Priority)
            .Where(task => task.UserId == currentUser.Id);

        if (!string.IsNullOrWhiteSpace(priority))
        {
            query = query.Where(task => task.Priority.Name == priority);
        }

        query = status?.Trim().ToLowerInvariant() switch
        {
            "active" => query.Where(task => !task.IsCompleted),
            "completed" => query.Where(task => task.IsCompleted),
            _ => query
        };

        var tasks = await query.ToListAsync();

        var response = tasks
            .Select(task => task.ToResponse(today))
            .Where(task => MatchesDueBucket(task, dueBucket))
            .OrderBy(task => GetBucketSortRank(task.DueBucket))
            .ThenBy(task => GetPrimaryDueDateSort(task))
            .ThenByDescending(task => GetPrimaryPrioritySort(task))
            .ThenBy(task => GetSecondaryDueDateSort(task))
            .ThenByDescending(task => GetSecondaryPrioritySort(task))
            .ThenByDescending(task => task.UpdatedAt)
            .ToArray();

        return ApiResults.Ok(response, httpContext.TraceIdentifier);
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        AppDbContext db,
        CurrentUser currentUser,
        IClock clock,
        ILoggerFactory loggerFactory,
        HttpContext httpContext)
    {
        var logger = loggerFactory.CreateLogger("Tasks");
        var task = await db.Tasks
            .AsNoTracking()
            .Include(task => task.Priority)
            .SingleOrDefaultAsync(task => task.Id == id && task.UserId == currentUser.Id);

        if (task is null)
        {
            logger.LogWarning("Task lookup failed for user {UserId}. TaskId: {TaskId}", currentUser.Id, id);
            return ApiResults.Error(StatusCodes.Status404NotFound, ApiError.NotFound("Task was not found."), httpContext.TraceIdentifier);
        }

        return ApiResults.Ok(task.ToResponse(GetToday(clock)), httpContext.TraceIdentifier);
    }

    private static async Task<IResult> CreateAsync(
        TaskCreateRequest request,
        AppDbContext db,
        CurrentUser currentUser,
        IClock clock,
        ILoggerFactory loggerFactory,
        HttpContext httpContext)
    {
        var logger = loggerFactory.CreateLogger("Tasks");
        logger.LogInformation("Task create attempt for user {UserId}.", currentUser.Id);

        var validationErrors = TaskValidation.ValidateCreate(request);
        if (validationErrors.Count > 0)
        {
            logger.LogWarning("Task create validation failed for user {UserId}.", currentUser.Id);
            return ApiResults.Error(StatusCodes.Status400BadRequest, ApiError.Validation(validationErrors), httpContext.TraceIdentifier);
        }

        var priority = await ResolvePriorityAsync(db, request.Priority ?? "Medium");
        if (priority is null)
        {
            logger.LogWarning("Task create priority validation failed for user {UserId}. Priority: {Priority}", currentUser.Id, request.Priority);
            return ApiResults.Error(
                StatusCodes.Status400BadRequest,
                ApiError.Validation(new Dictionary<string, string[]>
                {
                    ["priority"] = ["Priority must be Low, Medium, High, or Urgent."]
                }),
                httpContext.TraceIdentifier);
        }

        var now = clock.GetCurrentInstant();
        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            UserId = currentUser.Id,
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            PriorityId = priority.Id,
            Priority = priority,
            DueDate = request.DueDate!.Value,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        logger.LogInformation(
            "Task created for user {UserId}. TaskId: {TaskId}, Priority: {Priority}, DueDate: {DueDate}",
            currentUser.Id,
            task.Id,
            task.Priority.Name,
            task.DueDate);

        return ApiResults.Created($"/api/tasks/{task.Id}", task.ToResponse(GetToday(clock)), httpContext.TraceIdentifier);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        TaskUpdateRequest request,
        AppDbContext db,
        CurrentUser currentUser,
        IClock clock,
        ILoggerFactory loggerFactory,
        HttpContext httpContext)
    {
        var logger = loggerFactory.CreateLogger("Tasks");
        logger.LogInformation("Task update attempt for user {UserId}. TaskId: {TaskId}", currentUser.Id, id);

        var validationErrors = TaskValidation.ValidateUpdate(request);
        if (validationErrors.Count > 0)
        {
            logger.LogWarning("Task update validation failed for user {UserId}. TaskId: {TaskId}", currentUser.Id, id);
            return ApiResults.Error(StatusCodes.Status400BadRequest, ApiError.Validation(validationErrors), httpContext.TraceIdentifier);
        }

        var priority = await ResolvePriorityAsync(db, request.Priority);
        if (priority is null)
        {
            logger.LogWarning(
                "Task update priority validation failed for user {UserId}. TaskId: {TaskId}, Priority: {Priority}",
                currentUser.Id,
                id,
                request.Priority);
            return ApiResults.Error(
                StatusCodes.Status400BadRequest,
                ApiError.Validation(new Dictionary<string, string[]>
                {
                    ["priority"] = ["Priority must be Low, Medium, High, or Urgent."]
                }),
                httpContext.TraceIdentifier);
        }

        var task = await db.Tasks
            .Include(task => task.Priority)
            .SingleOrDefaultAsync(task => task.Id == id && task.UserId == currentUser.Id);
        if (task is null)
        {
            logger.LogWarning("Task update ownership/not-found failure for user {UserId}. TaskId: {TaskId}", currentUser.Id, id);
            return ApiResults.Error(StatusCodes.Status404NotFound, ApiError.NotFound("Task was not found."), httpContext.TraceIdentifier);
        }

        task.Title = request.Title.Trim();
        task.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        task.PriorityId = priority.Id;
        task.Priority = priority;
        task.DueDate = request.DueDate!.Value;
        task.UpdatedAt = clock.GetCurrentInstant();

        await db.SaveChangesAsync();

        logger.LogInformation("Task updated for user {UserId}. TaskId: {TaskId}", currentUser.Id, id);
        return ApiResults.Ok(task.ToResponse(GetToday(clock)), httpContext.TraceIdentifier);
    }

    private static async Task<IResult> CompleteAsync(
        Guid id,
        TaskCompletionRequest request,
        AppDbContext db,
        CurrentUser currentUser,
        IClock clock,
        ILoggerFactory loggerFactory,
        HttpContext httpContext)
    {
        var logger = loggerFactory.CreateLogger("Tasks");
        logger.LogInformation("Task completion update attempt for user {UserId}. TaskId: {TaskId}", currentUser.Id, id);

        var task = await db.Tasks
            .Include(task => task.Priority)
            .SingleOrDefaultAsync(task => task.Id == id && task.UserId == currentUser.Id);
        if (task is null)
        {
            logger.LogWarning("Task completion ownership/not-found failure for user {UserId}. TaskId: {TaskId}", currentUser.Id, id);
            return ApiResults.Error(StatusCodes.Status404NotFound, ApiError.NotFound("Task was not found."), httpContext.TraceIdentifier);
        }

        var now = clock.GetCurrentInstant();
        task.IsCompleted = request.IsCompleted;
        task.CompletedAt = request.IsCompleted ? now : null;
        task.UpdatedAt = now;

        await db.SaveChangesAsync();

        logger.LogInformation(
            "Task completion updated for user {UserId}. TaskId: {TaskId}, IsCompleted: {IsCompleted}",
            currentUser.Id,
            id,
            request.IsCompleted);

        return ApiResults.Ok(task.ToResponse(GetToday(clock)), httpContext.TraceIdentifier);
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        AppDbContext db,
        CurrentUser currentUser,
        ILoggerFactory loggerFactory,
        HttpContext httpContext)
    {
        var logger = loggerFactory.CreateLogger("Tasks");
        logger.LogInformation("Task delete attempt for user {UserId}. TaskId: {TaskId}", currentUser.Id, id);

        var task = await db.Tasks.SingleOrDefaultAsync(task => task.Id == id && task.UserId == currentUser.Id);
        if (task is null)
        {
            logger.LogWarning("Task delete ownership/not-found failure for user {UserId}. TaskId: {TaskId}", currentUser.Id, id);
            return ApiResults.Error(StatusCodes.Status404NotFound, ApiError.NotFound("Task was not found."), httpContext.TraceIdentifier);
        }

        db.Tasks.Remove(task);
        await db.SaveChangesAsync();

        logger.LogInformation("Task deleted for user {UserId}. TaskId: {TaskId}", currentUser.Id, id);
        return ApiResults.NoContent(httpContext.TraceIdentifier);
    }

    private static LocalDate GetToday(IClock clock)
    {
        return clock.GetCurrentInstant().InZone(DateTimeZoneProviders.Tzdb.GetSystemDefault()).Date;
    }

    private static int GetBucketSortRank(string dueBucket) =>
        dueBucket switch
        {
            "Overdue" => 0,
            "Today" => 1,
            "Upcoming" => 2,
            "Completed" => 3,
            _ => 4
        };

    private static int GetPrioritySortRank(string priority) =>
        priority switch
        {
            "Urgent" => TaskPriority.UrgentId,
            "High" => TaskPriority.HighId,
            "Medium" => TaskPriority.MediumId,
            "Low" => TaskPriority.LowId,
            _ => 0
        };

    private static bool MatchesDueBucket(TaskResponse task, string? dueBucket)
    {
        if (string.IsNullOrWhiteSpace(dueBucket) || dueBucket.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return task.DueBucket.Equals(dueBucket, StringComparison.OrdinalIgnoreCase);
    }

    private static LocalDate? GetPrimaryDueDateSort(TaskResponse task) =>
        task.DueBucket == "Overdue" ? task.DueDate : null;

    private static int GetPrimaryPrioritySort(TaskResponse task) =>
        task.DueBucket == "Overdue" ? 0 : GetPrioritySortRank(task.Priority);

    private static LocalDate? GetSecondaryDueDateSort(TaskResponse task) =>
        task.DueBucket == "Overdue" ? null : task.DueDate;

    private static int GetSecondaryPrioritySort(TaskResponse task) =>
        task.DueBucket == "Overdue" ? GetPrioritySortRank(task.Priority) : 0;

    private static async Task<TaskPriority?> ResolvePriorityAsync(AppDbContext db, string priority)
    {
        var normalized = priority.Trim();
        return await db.TaskPriorities.SingleOrDefaultAsync(item => item.Name == normalized);
    }
}
