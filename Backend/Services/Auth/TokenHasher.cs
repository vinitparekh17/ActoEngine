using System.Security.Cryptography;
using System.Text;

namespace ActoEngine.WebApi.Services.Auth;
public interface ITokenHasher
{
    string HashToken(string token);
}

public class TokenHasher : ITokenHasher
{
    public string HashToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("Token cannot be null or empty.", nameof(token));
        }

        byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim()));
        return Convert.ToBase64String(hashBytes);
    }
}
