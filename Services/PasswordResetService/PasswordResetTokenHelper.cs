using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Services.PasswordResetService
{
    public static class PasswordResetTokenHelper
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
