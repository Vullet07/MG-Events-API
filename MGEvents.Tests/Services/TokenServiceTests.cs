using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Services.JwtService;

namespace MGEvents.Tests.Services;

public class TokenServiceTests
{
    private readonly JwtSettings _settings = new()
    {
        Key = "test-signing-key-with-enough-length-1234567890",
        Issuer = "mg-events-tests",
        Audience = "mg-events-tests-client",
        ExpiresInMinutes = 60
    };

    [Fact]
    public void GenerateToken_ShouldIncludeExpectedClaims()
    {
        var service = new TokenService(Options.Create(_settings));

        var result = service.GenerateToken("42", "Admin", "Vasko");
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);

        Assert.Equal("42", jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal("Vasko", jwt.Claims.First(c => c.Type == ClaimTypes.Name).Value);
        Assert.Equal("Admin", jwt.Claims.First(c => c.Type == ClaimTypes.Role).Value);
    }

    [Fact]
    public void GenerateToken_ShouldSetExpirationUsingConfiguredMinutes()
    {
        var service = new TokenService(Options.Create(_settings));
        var beforeCall = DateTime.UtcNow;

        var result = service.GenerateToken("42", "Student", "User");

        Assert.True(result.ExpiresAt > beforeCall);
        Assert.InRange(result.ExpiresAt, beforeCall.AddMinutes(59), beforeCall.AddMinutes(61));
    }

    [Fact]
    public void GenerateToken_ShouldCreateTokenThatValidatesAgainstConfiguredParameters()
    {
        var service = new TokenService(Options.Create(_settings));
        var result = service.GenerateToken("11", "Teacher", "Mentor");

        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _settings.Issuer,
            ValidateAudience = true,
            ValidAudience = _settings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(_settings.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(5)
        };

        var principal = new JwtSecurityTokenHandler().ValidateToken(result.Token, tokenValidationParameters, out _);

        var subject =
            principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
            principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var role =
            principal.FindFirst(ClaimTypes.Role)?.Value ??
            principal.FindFirst("role")?.Value;

        Assert.Equal("11", subject);
        Assert.Equal("Teacher", role);
    }
}
