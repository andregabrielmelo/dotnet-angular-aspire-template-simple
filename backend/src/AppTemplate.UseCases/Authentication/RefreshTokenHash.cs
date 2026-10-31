using System.Security.Cryptography;
using System.Text;

namespace AppTemplate.UseCases.Authentication;

/// <summary>
/// Refresh tokens are stored as their SHA-256 hash, so a database leak hands out no usable
/// session. A plain hash is enough: the tokens are 64 random bytes, not guessable passwords.
/// </summary>
public static class RefreshTokenHash
{
    public static string Of(string refreshToken) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
}
