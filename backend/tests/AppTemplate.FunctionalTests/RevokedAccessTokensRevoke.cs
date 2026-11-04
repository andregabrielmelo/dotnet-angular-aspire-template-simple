using AppTemplate.Web.Authentication;
using Microsoft.Extensions.Caching.Memory;

namespace AppTemplate.FunctionalTests;

/// <summary>The in-memory revocation list on its own; no Docker.</summary>
public class RevokedAccessTokensRevoke
{
    private readonly RevokedAccessTokens _revoked = new(new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public void MarksTheTokenRevokedUntilItExpires()
    {
        _revoked.Revoke("jti-1", DateTimeOffset.UtcNow.AddMinutes(15));

        _revoked.IsRevoked("jti-1").ShouldBeTrue();
        _revoked.IsRevoked("jti-2").ShouldBeFalse();
    }

    [Fact]
    public void ForgetsATokenThatAlreadyExpired()
    {
        _revoked.Revoke("jti-1", DateTimeOffset.UtcNow.AddSeconds(-1));

        _revoked.IsRevoked("jti-1").ShouldBeFalse();
    }
}
