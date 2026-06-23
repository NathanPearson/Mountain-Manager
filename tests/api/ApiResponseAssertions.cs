using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;

namespace MountainManager.Api.Tests;

public static class ApiResponseAssertions
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static async Task<ApiEnvelope<T>> ReadEnvelopeAsync<T>(this HttpResponseMessage response)
    {
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<T>>(JsonOptions);
        envelope.Should().NotBeNull();
        return envelope!;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        options.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
        return options;
    }
}

public sealed record ApiEnvelope<T>(bool Success, T? Data, ApiErrorEnvelope? Error, string TraceId);
public sealed record ApiErrorEnvelope(string Code, string Message, Dictionary<string, string[]>? Details);
