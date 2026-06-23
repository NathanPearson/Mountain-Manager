using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace MountainManager.Api.Tests;

[Collection(ApiTestCollection.Name)]
public sealed class AuthApiTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task RegisterRejectsInvalidEmailAndPassword()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "not-an-email",
            password = "short"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var envelope = await response.ReadEnvelopeAsync<object>();
        envelope.Success.Should().BeFalse();
        envelope.Error!.Code.Should().Be("VALIDATION_ERROR");
        envelope.Error.Details.Should().ContainKey("email");
        envelope.Error.Details.Should().ContainKey("password");
        envelope.TraceId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RegisterRejectsMissingEmailWithoutThrowing()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            password = "Password123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var envelope = await response.ReadEnvelopeAsync<object>();
        envelope.Success.Should().BeFalse();
        envelope.Error!.Code.Should().Be("VALIDATION_ERROR");
        envelope.Error.Details.Should().ContainKey("email");
    }

    [Fact]
    public async Task LoginRejectsMissingEmailWithoutThrowing()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            password = "Password123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var envelope = await response.ReadEnvelopeAsync<object>();
        envelope.Success.Should().BeFalse();
        envelope.Error!.Code.Should().Be("VALIDATION_ERROR");
        envelope.Error.Details.Should().ContainKey("email");
    }

    [Fact]
    public async Task LoginRejectsInvalidCredentials()
    {
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "login-rejects-invalid-credentials@example.com",
            password = "Password123!"
        });

        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "login-rejects-invalid-credentials@example.com",
            password = "WrongPassword123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var envelope = await response.ReadEnvelopeAsync<object>();
        envelope.Success.Should().BeFalse();
        envelope.Error!.Code.Should().Be("UNAUTHORIZED");
        envelope.Error.Message.Should().Be("Invalid email or password.");
        envelope.TraceId.Should().NotBeNullOrWhiteSpace();
    }
}
