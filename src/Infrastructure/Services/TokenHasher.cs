using ActoX.Application.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace ActoX.Infrastructure.Services
{
    public class TokenHasher : ITokenHasher
    {
        public string HashToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentException("Token cannot be null or empty.", nameof(token));
            byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim()));
            return Convert.ToBase64String(hashBytes);
        }
    }
}