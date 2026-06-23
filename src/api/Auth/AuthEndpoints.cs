using Microsoft.EntityFrameworkCore;
using MountainManager.Api.Common;
using MountainManager.Api.Data;
using NodaTime;

namespace MountainManager.Api.Auth;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", RegisterAsync);
        group.MapPost("/login", LoginAsync);
        group.MapGet("/me", Me).RequireAuthorization();

        return group;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        AppDbContext db,
        PasswordHasher passwordHasher,
        JwtTokenService jwtTokenService,
        IClock clock,
        ILoggerFactory loggerFactory,
        HttpContext httpContext)
    {
        var logger = loggerFactory.CreateLogger("Auth");
        var email = request.Email.Trim().ToLowerInvariant();

        logger.LogInformation("Registration attempt for email {Email}.", email);

        var validationErrors = ValidateCredentials(email, request.Password);
        if (validationErrors.Count > 0)
        {
            logger.LogWarning("Registration validation failed for email {Email}.", email);
            return ApiResults.Error(StatusCodes.Status400BadRequest, ApiError.Validation(validationErrors), httpContext.TraceIdentifier);
        }

        if (await db.Users.AnyAsync(user => user.Email == email))
        {
            logger.LogWarning("Registration conflict for existing email {Email}.", email);
            return ApiResults.Error(StatusCodes.Status409Conflict, ApiError.Conflict("An account already exists for this email."), httpContext.TraceIdentifier);
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = passwordHasher.Hash(request.Password),
            CreatedAt = clock.GetCurrentInstant()
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        logger.LogInformation("Registration succeeded for user {UserId}.", user.Id);

        var response = jwtTokenService.CreateToken(user);
        return ApiResults.Created("/api/auth/me", response, httpContext.TraceIdentifier);
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        AppDbContext db,
        PasswordHasher passwordHasher,
        JwtTokenService jwtTokenService,
        ILoggerFactory loggerFactory,
        HttpContext httpContext)
    {
        var logger = loggerFactory.CreateLogger("Auth");
        var email = request.Email.Trim().ToLowerInvariant();

        logger.LogInformation("Login attempt for email {Email}.", email);

        var user = await db.Users.SingleOrDefaultAsync(user => user.Email == email);
        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            logger.LogWarning("Login failed for email {Email}.", email);
            return ApiResults.Error(StatusCodes.Status401Unauthorized, ApiError.Unauthorized("Invalid email or password."), httpContext.TraceIdentifier);
        }

        logger.LogInformation("Login succeeded for user {UserId}.", user.Id);

        var response = jwtTokenService.CreateToken(user);
        return ApiResults.Ok(response, httpContext.TraceIdentifier);
    }

    private static IResult Me(CurrentUser currentUser, HttpContext httpContext)
    {
        return ApiResults.Ok(new UserResponse(currentUser.Id, currentUser.Email ?? string.Empty), httpContext.TraceIdentifier);
    }

    private static Dictionary<string, string[]> ValidateCredentials(string email, string password)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            errors["email"] = ["A valid email is required."];
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            errors["password"] = ["Password must be at least 8 characters."];
        }

        return errors;
    }
}
