using AppTemplate.Core.Aggregates.UserAggregate;

namespace AppTemplate.Core.Aggregates.RefreshTokenAggregate;

/// <summary>
/// A refresh token a user signed in with. Only its SHA-256 hash is stored, never the token itself,
/// so a database leak hands out no usable session. Each token is used once: refreshing deletes it
/// and stores the next one.
/// </summary>
public class RefreshToken : EntityBase<RefreshToken, RefreshTokenId>, IAggregateRoot
{
    /// <summary>A SHA-256 hash, hex-encoded.</summary>
    public const int TokenHashLength = 64;

    private RefreshToken() { } // EF Core

    private RefreshToken(UserId userId, string tokenHash, DateTimeOffset expiresAtUtc)
    {
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAtUtc = expiresAtUtc;
    }

    public UserId UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public static RefreshToken Create(UserId userId, string tokenHash, DateTimeOffset expiresAtUtc)
    {
        if (tokenHash.Length != TokenHashLength)
            throw new ArgumentException(
                $"Token hash must be {TokenHashLength} hex characters",
                nameof(tokenHash)
            );

        return new RefreshToken(userId, tokenHash, expiresAtUtc);
    }

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAtUtc;
}
