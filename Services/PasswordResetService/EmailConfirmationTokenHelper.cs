using System.Security.Cryptography;
using System.Text;

namespace Services.PasswordResetService
{
    public static class EmailConfirmationTokenHelper
    {
        public static string GenerateToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            return Convert.ToBase64String(bytes);
        }

        public static string HashToken(string token)
        {
            using var sha = SHA256.Create();
            return Convert.ToBase64String(
                sha.ComputeHash(Encoding.UTF8.GetBytes(token))
            );
        }
    }
}
