using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;

namespace MountainManager.Api.Tests;

public sealed class ApiTestFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"mountain-manager-tests-{Guid.NewGuid():N}.db");

    public ApiTestFactory()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", $"Data Source={_databasePath}");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "MountainManager");
        Environment.SetEnvironmentVariable("Jwt__Audience", "MountainManager");
        Environment.SetEnvironmentVariable("Jwt__SigningKey", "development-only-signing-key-change-before-production-please");
        Environment.SetEnvironmentVariable("Jwt__ExpirationMinutes", "60");
        Environment.SetEnvironmentVariable("Cors__AllowedOrigins__0", "http://localhost:5173");
        Environment.SetEnvironmentVariable("Cors__AllowedOrigins__1", "http://127.0.0.1:5173");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration(configuration =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = $"Data Source={_databasePath}",
                ["Jwt:Issuer"] = "MountainManager",
                ["Jwt:Audience"] = "MountainManager",
                ["Jwt:SigningKey"] = "development-only-signing-key-change-before-production-please",
                ["Jwt:ExpirationMinutes"] = "60",
                ["Cors:AllowedOrigins:0"] = "http://localhost:5173"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IClock>(new FixedClock(Instant.FromUtc(2030, 6, 18, 12, 0)));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        Environment.SetEnvironmentVariable("ConnectionStrings__Default", null);
        Environment.SetEnvironmentVariable("Jwt__Issuer", null);
        Environment.SetEnvironmentVariable("Jwt__Audience", null);
        Environment.SetEnvironmentVariable("Jwt__SigningKey", null);
        Environment.SetEnvironmentVariable("Jwt__ExpirationMinutes", null);
        Environment.SetEnvironmentVariable("Cors__AllowedOrigins__0", null);
        Environment.SetEnvironmentVariable("Cors__AllowedOrigins__1", null);

        try
        {
            if (File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
            }
        }
        catch (IOException)
        {
            // Windows can keep the SQLite file handle briefly after the test host is disposed.
        }
    }
}

internal sealed class FixedClock(Instant instant) : IClock
{
    public Instant GetCurrentInstant() => instant;
}
