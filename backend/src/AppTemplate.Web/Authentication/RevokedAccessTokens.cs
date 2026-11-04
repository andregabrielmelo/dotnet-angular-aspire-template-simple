using Microsoft.Extensions.Caching.Memory;

namespace AppTemplate.Web.Authentication;

/// <summary>
/// Access tokens signed out before they expired, by <c>jti</c>, checked by
/// <see cref="RevokedTokenChecker"/>. In memory, as FastEndpoints' docs allow ("a database or
/// cache"): validation stays stateless, with no store to query or depend on. Each entry expires
/// with its token. Per instance and lost on restart: there, a signed-out token works until it
/// expires (at most the access token validity). Use IDistributedCache with several instances.
/// </summary>
public sealed class RevokedAccessTokens(IMemoryCache cache)
{
    public void Revoke(string jti, DateTimeOffset expiresAt)
    {
        if (expiresAt > DateTimeOffset.UtcNow)
        {
            cache.Set(Key(jti), true, expiresAt);
        }
    }

    public bool IsRevoked(string jti) => cache.TryGetValue(Key(jti), out _);

    private static string Key(string jti) => $"revoked-access-token:{jti}";
}
