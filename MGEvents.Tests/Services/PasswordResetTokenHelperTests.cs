using System.Security.Cryptography;
using System.Text;
using Services.PasswordResetService;

namespace MGEvents.Tests.Services;

public class PasswordResetTokenHelperTests
{
    [Fact]
    public void GenerateToken_ShouldReturnBase64Encoded32Bytes()
    {
        var token = PasswordResetTokenHelper.GenerateToken();

        var bytes = Convert.FromBase64String(token);
        Assert.Equal(32, bytes.Length);
    }

    [Fact]
    public void GenerateToken_ShouldProduceDifferentTokens()
    {
        var first = PasswordResetTokenHelper.GenerateToken();
        var second = PasswordResetTokenHelper.GenerateToken();

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void HashToken_ShouldReturnDeterministicSha256Hash()
    {
        const string rawToken = "sample-token";

        var actual = PasswordResetTokenHelper.HashToken(rawToken);

        using var sha = SHA256.Create();
        var expected = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(rawToken)));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void HashToken_ShouldProduceDifferentHashes_ForDifferentInputs()
    {
        var first = PasswordResetTokenHelper.HashToken("token-1");
        var second = PasswordResetTokenHelper.HashToken("token-2");

        Assert.NotEqual(first, second);
    }
}
