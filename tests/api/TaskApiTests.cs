using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MountainManager.Api.Data;

namespace MountainManager.Api.Tests;

[Collection(ApiTestCollection.Name)]
public sealed class TaskApiTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory = factory;
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task CreateTaskRejectsEmptyTitle()
    {
        await AuthenticateAsync("empty-title@example.com");

        var response = await _client.PostAsJsonAsync("/api/tasks", new
        {
            title = "",
            description = "Nope",
            priority = "High",
            dueDate = "2030-06-20"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var envelope = await response.ReadEnvelopeAsync<object>();
        envelope.Success.Should().BeFalse();
        envelope.Error!.Code.Should().Be("VALIDATION_ERROR");
        envelope.Error.Details.Should().ContainKey("title");
        envelope.TraceId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CreateTaskRejectsMissingDueDate()
    {
        await AuthenticateAsync("missing-date@example.com");

        var response = await _client.PostAsJsonAsync("/api/tasks", new
        {
            title = "Schedule labs",
            description = "Call the clinic",
            priority = "Medium"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var envelope = await response.ReadEnvelopeAsync<object>();
        envelope.Success.Should().BeFalse();
        envelope.Error!.Details.Should().ContainKey("dueDate");
    }

    [Fact]
    public async Task CreateTaskRejectsUnknownPriority()
    {
        await AuthenticateAsync("unknown-priority@example.com");

        var response = await _client.PostAsJsonAsync("/api/tasks", new
        {
            title = "Bad priority",
            description = "Priority should not resolve.",
            priority = "Critical",
            dueDate = "2030-06-20"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var envelope = await response.ReadEnvelopeAsync<object>();
        envelope.Success.Should().BeFalse();
        envelope.Error!.Code.Should().Be("VALIDATION_ERROR");
        envelope.Error.Details.Should().ContainKey("priority");
        envelope.TraceId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CreateTaskDefaultsPriorityToMedium()
    {
        await AuthenticateAsync("default-priority@example.com");

        var response = await _client.PostAsJsonAsync("/api/tasks", new
        {
            title = "Review imaging prep",
            description = "Confirm checklist",
            dueDate = "2030-06-20"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var envelope = await response.ReadEnvelopeAsync<TaskResponse>();
        envelope.Success.Should().BeTrue();
        envelope.Data!.Priority.Should().Be("Medium");
        envelope.Data.DueDate.Should().Be("2030-06-20");
        envelope.Data.DueBucket.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CreateTaskRejectsDuplicateTitleForSameUser()
    {
        await AuthenticateAsync("duplicate-title@example.com");

        await _client.PostAsJsonAsync("/api/tasks", new
        {
            title = "Schedule labs",
            description = "Original",
            priority = "Medium",
            dueDate = "2030-06-20"
        });

        var response = await _client.PostAsJsonAsync("/api/tasks", new
        {
            title = " schedule labs ",
            description = "Duplicate with different casing and whitespace",
            priority = "High",
            dueDate = "2030-06-21"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var envelope = await response.ReadEnvelopeAsync<object>();
        envelope.Success.Should().BeFalse();
        envelope.Error!.Code.Should().Be("VALIDATION_ERROR");
        envelope.Error.Details.Should().ContainKey("title");
        envelope.Error.Details!["title"].Should().Contain("A task with this title already exists.");
    }

    [Fact]
    public async Task CreateTaskAllowsSameTitleForDifferentUsers()
    {
        await AuthenticateAsync("duplicate-owner@example.com");

        await _client.PostAsJsonAsync("/api/tasks", new
        {
            title = "Shared task title",
            priority = "Medium",
            dueDate = "2030-06-20"
        });

        await AuthenticateAsync("duplicate-other-user@example.com");

        var response = await _client.PostAsJsonAsync("/api/tasks", new
        {
            title = "Shared task title",
            priority = "Medium",
            dueDate = "2030-06-20"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task PrioritiesAreStoredAsLookupRowsAndTasksReferencePriorityId()
    {
        await AuthenticateAsync("priority-lookup@example.com");

        var response = await _client.PostAsJsonAsync("/api/tasks", new
        {
            title = "Lookup-backed priority",
            description = "Should store High as PriorityId 3.",
            priority = "High",
            dueDate = "2030-06-20"
        });

        var created = (await response.ReadEnvelopeAsync<TaskResponse>()).Data!;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var priorities = await db.TaskPriorities
            .AsNoTracking()
            .OrderBy(priority => priority.Id)
            .Select(priority => new { priority.Id, priority.Name, priority.SortRank })
            .ToListAsync();

        priorities.Should().BeEquivalentTo(
            [
                new { Id = 1, Name = "Low", SortRank = 1 },
                new { Id = 2, Name = "Medium", SortRank = 2 },
                new { Id = 3, Name = "High", SortRank = 3 },
                new { Id = 4, Name = "Urgent", SortRank = 4 }
            ],
            options => options.WithStrictOrdering());

        var task = await db.Tasks.AsNoTracking().SingleAsync(task => task.Id == created.Id);
        task.PriorityId.Should().Be(TaskPriority.HighId);
    }

    [Fact]
    public async Task ListSortsByPriorityUrgencyWithinEachDueBucket()
    {
        await AuthenticateAsync("priority-sort@example.com");

        await _client.PostAsJsonAsync("/api/tasks", new
        {
            title = "Today low",
            priority = "Low",
            dueDate = "2030-06-18"
        });

        await _client.PostAsJsonAsync("/api/tasks", new
        {
            title = "Today urgent",
            priority = "Urgent",
            dueDate = "2030-06-18"
        });

        await _client.PostAsJsonAsync("/api/tasks", new
        {
            title = "Today high",
            priority = "High",
            dueDate = "2030-06-18"
        });

        var response = await _client.GetAsync("/api/tasks?status=active");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var envelope = await response.ReadEnvelopeAsync<TaskResponse[]>();
        envelope.Data!
            .Where(task => task.DueBucket == "Today")
            .Select(task => task.Title)
            .Should()
            .Equal("Today urgent", "Today high", "Today low");
    }

    [Fact]
    public async Task OverdueTasksSortByOldestDueDateThenPriorityUrgency()
    {
        await AuthenticateAsync("overdue-sort@example.com");

        await _client.PostAsJsonAsync("/api/tasks", new
        {
            title = "Older overdue low",
            priority = "Low",
            dueDate = "2030-06-16"
        });

        await _client.PostAsJsonAsync("/api/tasks", new
        {
            title = "Newer overdue urgent",
            priority = "Urgent",
            dueDate = "2030-06-17"
        });

        await _client.PostAsJsonAsync("/api/tasks", new
        {
            title = "Older overdue high",
            priority = "High",
            dueDate = "2030-06-16"
        });

        var response = await _client.GetAsync("/api/tasks?status=active");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var envelope = await response.ReadEnvelopeAsync<TaskResponse[]>();
        envelope.Data!
            .Where(task => task.DueBucket == "Overdue")
            .Select(task => task.Title)
            .Should()
            .Equal("Older overdue high", "Older overdue low", "Newer overdue urgent");
    }

    [Fact]
    public async Task ListCanFilterByDueBucket()
    {
        await AuthenticateAsync("due-bucket-filter@example.com");

        await _client.PostAsJsonAsync("/api/tasks", new
        {
            title = "Filter overdue",
            priority = "High",
            dueDate = "2030-06-17"
        });

        await _client.PostAsJsonAsync("/api/tasks", new
        {
            title = "Filter today",
            priority = "High",
            dueDate = "2030-06-18"
        });

        await _client.PostAsJsonAsync("/api/tasks", new
        {
            title = "Filter upcoming",
            priority = "High",
            dueDate = "2030-06-25"
        });

        var response = await _client.GetAsync("/api/tasks?dueBucket=Upcoming");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var envelope = await response.ReadEnvelopeAsync<TaskResponse[]>();
        envelope.Data!.Select(task => task.Title).Should().Equal("Filter upcoming");
    }

    [Fact]
    public async Task DueBucketUsesClientTimeZoneHeaderForToday()
    {
        await AuthenticateAsync("time-zone@example.com");
        _client.DefaultRequestHeaders.Add("X-Time-Zone", "Pacific/Kiritimati");

        var response = await _client.PostAsJsonAsync("/api/tasks", new
        {
            title = "Client-local today",
            priority = "High",
            dueDate = "2030-06-19"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var envelope = await response.ReadEnvelopeAsync<TaskResponse>();
        envelope.Data!.DueBucket.Should().Be("Today");
    }

    [Fact]
    public async Task CrudFlowWorksForAuthenticatedUser()
    {
        await AuthenticateAsync("crud@example.com");

        var createResponse = await _client.PostAsJsonAsync("/api/tasks", new
        {
            title = "Initial title",
            description = "Initial notes",
            priority = "Low",
            dueDate = "2030-06-20"
        });

        var created = (await createResponse.ReadEnvelopeAsync<TaskResponse>()).Data!;
        createResponse.Headers.Location.Should().Be(new Uri($"/api/tasks/{created.Id}", UriKind.Relative));

        var updateResponse = await _client.PutAsJsonAsync($"/api/tasks/{created.Id}", new
        {
            title = "Updated title",
            description = "Updated notes",
            priority = "Urgent",
            dueDate = "2030-06-21"
        });

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = (await updateResponse.ReadEnvelopeAsync<TaskResponse>()).Data!;
        updated.Title.Should().Be("Updated title");
        updated.Priority.Should().Be("Urgent");
        updated.DueDate.Should().Be("2030-06-21");

        var completeResponse = await _client.PatchAsJsonAsync($"/api/tasks/{created.Id}/completion", new
        {
            isCompleted = true
        });

        completeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var completed = (await completeResponse.ReadEnvelopeAsync<TaskResponse>()).Data!;
        completed.IsCompleted.Should().BeTrue();
        completed.DueBucket.Should().Be("Completed");

        var deleteResponse = await _client.DeleteAsync($"/api/tasks/{created.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getAfterDelete = await _client.GetAsync($"/api/tasks/{created.Id}");
        getAfterDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateTaskRejectsDuplicateTitleForSameUser()
    {
        await AuthenticateAsync("duplicate-update@example.com");

        await _client.PostAsJsonAsync("/api/tasks", new
        {
            title = "Existing title",
            priority = "Medium",
            dueDate = "2030-06-20"
        });

        var createResponse = await _client.PostAsJsonAsync("/api/tasks", new
        {
            title = "Title to update",
            priority = "Low",
            dueDate = "2030-06-21"
        });

        var created = (await createResponse.ReadEnvelopeAsync<TaskResponse>()).Data!;

        var updateResponse = await _client.PutAsJsonAsync($"/api/tasks/{created.Id}", new
        {
            title = " existing title ",
            description = "Should fail",
            priority = "Low",
            dueDate = "2030-06-22"
        });

        updateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var envelope = await updateResponse.ReadEnvelopeAsync<object>();
        envelope.Success.Should().BeFalse();
        envelope.Error!.Code.Should().Be("VALIDATION_ERROR");
        envelope.Error.Details.Should().ContainKey("title");
        envelope.Error.Details!["title"].Should().Contain("A task with this title already exists.");
    }

    [Fact]
    public async Task UserCannotReadUpdateOrDeleteAnotherUsersTask()
    {
        await AuthenticateAsync("owner@example.com");

        var createResponse = await _client.PostAsJsonAsync("/api/tasks", new
        {
            title = "Private task",
            description = "Belongs to owner",
            priority = "High",
            dueDate = "2030-06-20"
        });

        var created = (await createResponse.ReadEnvelopeAsync<TaskResponse>()).Data!;

        await AuthenticateAsync("intruder@example.com");

        var readResponse = await _client.GetAsync($"/api/tasks/{created.Id}");
        readResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var updateResponse = await _client.PutAsJsonAsync($"/api/tasks/{created.Id}", new
        {
            title = "Changed",
            description = "Should not work",
            priority = "Low",
            dueDate = "2030-06-22"
        });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var deleteResponse = await _client.DeleteAsync($"/api/tasks/{created.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task AuthenticateAsync(string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Password123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var envelope = await response.ReadEnvelopeAsync<AuthResponse>();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", envelope.Data!.Token);
    }
}

public sealed record AuthResponse(string Token, UserResponse User, string ExpiresAt);
public sealed record UserResponse(Guid Id, string Email);
public sealed record TaskResponse(
    Guid Id,
    string Title,
    string? Description,
    string Priority,
    string DueDate,
    string DueBucket,
    bool IsCompleted,
    string CreatedAt,
    string UpdatedAt,
    string? CompletedAt);
