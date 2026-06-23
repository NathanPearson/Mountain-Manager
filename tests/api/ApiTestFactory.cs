using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MountainManager.Api.Data;
using NodaTime;

namespace MountainManager.Api.Tests;

public sealed class ApiTestFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"mountain-manager-tests-{Guid.NewGuid():N}.db");

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
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlite($"Data Source={_databasePath}");
            });

            services.AddSingleton<IClock>(new FixedClock(Instant.FromUtc(2030, 6, 18, 12, 0)));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

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
