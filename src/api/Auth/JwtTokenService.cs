using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MountainManager.Api.Data;
using NodaTime;

namespace MountainManager.Api.Auth;

public sealed class JwtTokenService(IConfiguration configuration, IClock clock)
{
    public AuthResponse CreateToken(User user)
    {
        var settings = configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
        var expiresAt = clock.GetCurrentInstant().Plus(Duration.FromMinutes(settings.ExpirationMinutes));
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email)
        };

        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims: claims,
            expires: expiresAt.ToDateTimeUtc(),
            signingCredentials: credentials);

        return new AuthResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            new UserResponse(user.Id, user.Email),
            expiresAt);
    }
}
