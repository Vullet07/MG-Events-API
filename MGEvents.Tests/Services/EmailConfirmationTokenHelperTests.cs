using System.Security.Cryptography;
using System.Text;
using Services.PasswordResetService;

namespace MGEvents.Tests.Services;

public class EmailConfirmationTokenHelperTests
{
    [Fact]
    public void GenerateToken_ReturnsBase64Encoded32Bytes()
    {
        var token = EmailConfirmationTokenHelper.GenerateToken();

        var bytes = Convert.FromBase64String(token);
        Assert.Equal(32, bytes.Length);
    }

    [Fact]
    public void GenerateToken_ReturnsDifferentValuesPerCall()
    {
        var first = EmailConfirmationTokenHelper.GenerateToken();
        var second = EmailConfirmationTokenHelper.GenerateToken();

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void HashToken_ReturnsDeterministicSha256Hash()
    {
        const string rawToken = "confirm-me";

        var actual = EmailConfirmationTokenHelper.HashToken(rawToken);

        using var sha = SHA256.Create();
        var expected = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(rawToken)));
        Assert.Equal(expected, actual);
    }
}
