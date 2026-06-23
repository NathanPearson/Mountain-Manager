using NodaTime;

namespace MountainManager.Api.Auth;

public sealed record RegisterRequest(string Email, string Password);
public sealed record LoginRequest(string Email, string Password);
public sealed record AuthResponse(string Token, UserResponse User, Instant ExpiresAt);
public sealed record UserResponse(Guid Id, string Email);
