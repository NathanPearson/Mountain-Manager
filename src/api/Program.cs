using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MountainManager.Api.Auth;
using MountainManager.Api.Common;
using MountainManager.Api.Data;
using MountainManager.Api.Tasks;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Mountain Manager API",
        Version = "v1",
        Description = "Task management API with authentication, priority, and date-only due dates."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter a JWT from /api/auth/login or /api/auth/register."
    });

    options.OperationFilter<BearerSecurityOperationFilter>();
});

builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddSingleton<IClock>(SystemClock.Instance);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<PasswordHasher>();
builder.Services.AddScoped<CurrentUser>();

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Data Source=mountain-manager.db";

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlite(connectionString);
});

var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SigningKey));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = signingKey,
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        options.Events = new JwtBearerEvents
        {
            OnChallenge = context =>
            {
                context.HandleResponse();
                return ApiResults.WriteErrorAsync(
                    context.HttpContext,
                    StatusCodes.Status401Unauthorized,
                    ApiError.Unauthorized("Authentication is required."));
            },
            OnForbidden = context =>
                ApiResults.WriteErrorAsync(
                    context.HttpContext,
                    StatusCodes.Status403Forbidden,
                    ApiError.Forbidden("You do not have permission to access this resource."))
        };
    });

builder.Services.AddAuthorization();
builder.Services.Configure<RouteHandlerOptions>(options =>
{
    options.ThrowOnBadRequest = true;
});

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseStatusCodePages(async statusCodeContext =>
{
    var httpContext = statusCodeContext.HttpContext;

    if (!httpContext.Request.Path.StartsWithSegments("/api") || httpContext.Response.HasStarted)
    {
        return;
    }

    var error = httpContext.Response.StatusCode switch
    {
        StatusCodes.Status404NotFound => ApiError.NotFound("The requested API endpoint was not found."),
        StatusCodes.Status401Unauthorized => ApiError.Unauthorized("Authentication is required."),
        StatusCodes.Status403Forbidden => ApiError.Forbidden("You do not have permission to access this resource."),
        _ => ApiError.Unexpected()
    };

    await ApiResults.WriteErrorAsync(httpContext, httpContext.Response.StatusCode, error);
});

if (app.Environment.IsDevelopment())
{
    app.MapSwagger("/openapi/{documentName}.json");
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("Mountain Manager API");
    });
}

app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();

using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    logger.LogInformation("Initializing SQLite database.");
    db.Database.EnsureCreated();
    logger.LogInformation("SQLite database initialization complete.");
}

app.MapGet("/api/health", (HttpContext httpContext) =>
    ApiResults.Ok(new { status = "Healthy" }, httpContext.TraceIdentifier));

app.MapAuthEndpoints();
app.MapTaskEndpoints();

app.Run();

public partial class Program;
