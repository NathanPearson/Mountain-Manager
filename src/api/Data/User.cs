using NodaTime;

namespace MountainManager.Api.Data;

public sealed class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public Instant CreatedAt { get; set; }
}
