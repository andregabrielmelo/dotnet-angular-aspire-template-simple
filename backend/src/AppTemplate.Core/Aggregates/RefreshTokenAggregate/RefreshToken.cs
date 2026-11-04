using AppTemplate.Core.Aggregates.UserAggregate;

namespace AppTemplate.Core.Aggregates.RefreshTokenAggregate;

/// <summary>
/// A refresh token a user signed in with. Only its SHA-256 hash is stored, never the token itself,
/// so a database leak hands out no usable session. Each token is used once: refreshing deletes it
/// and stores the next one. It also records the account's security stamp when it was issued: a
/// password change or reset changes the stamp, which ends every session at its next refresh.
/// </summary>
public class RefreshToken : EntityBase<RefreshToken, RefreshTokenId>, IAggregateRoot
{
    /// <summary>A SHA-256 hash, hex-encoded.</summary>
    public const int TokenHashLength = 64;

    public const int SecurityStampMaxLength = 256;

    private RefreshToken() { } // EF Core

    private RefreshToken(
        UserId userId,
        string tokenHash,
        string securityStamp,
        DateTimeOffset expiresAtUtc
    )
    {
        UserId = userId;
        TokenHash = tokenHash;
        SecurityStamp = securityStamp;
        ExpiresAtUtc = expiresAtUtc;
    }

    public UserId UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;

    /// <summary>The account's security stamp when the token was issued.</summary>
    public string SecurityStamp { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public static RefreshToken Create(
        UserId userId,
        string tokenHash,
        string securityStamp,
        DateTimeOffset expiresAtUtc
    )
    {
        if (tokenHash.Length != TokenHashLength)
            throw new ArgumentException(
                $"Token hash must be {TokenHashLength} hex characters",
                nameof(tokenHash)
            );

        if (string.IsNullOrEmpty(securityStamp) || securityStamp.Length > SecurityStampMaxLength)
            throw new ArgumentException(
                $"Security stamp must be 1 to {SecurityStampMaxLength} characters",
                nameof(securityStamp)
            );

        return new RefreshToken(userId, tokenHash, securityStamp, expiresAtUtc);
    }

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAtUtc;

    /// <summary>The account's password (or anything else guarded by its stamp) changed since.</summary>
    public bool IsStale(string currentSecurityStamp) =>
        !string.Equals(SecurityStamp, currentSecurityStamp, StringComparison.Ordinal);
}
