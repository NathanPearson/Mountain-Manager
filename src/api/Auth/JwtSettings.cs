namespace MountainManager.Api.Auth;

public sealed class JwtSettings
{
    public string Issuer { get; set; } = "MountainManager";
    public string Audience { get; set; } = "MountainManager";
    public string SigningKey { get; set; } = "development-only-signing-key-change-before-production";
    public int ExpirationMinutes { get; set; } = 120;
}
