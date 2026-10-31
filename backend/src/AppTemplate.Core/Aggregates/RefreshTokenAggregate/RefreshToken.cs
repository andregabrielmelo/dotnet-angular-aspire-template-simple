using AppTemplate.Core.Aggregates.UserAggregate;

namespace AppTemplate.Core.Aggregates.RefreshTokenAggregate;

/// <summary>
/// One refresh token of a sign-in session. Every sign-in starts a <see cref="FamilyId"/>; each
/// refresh rotates the token, revoking it and issuing the next one in the same family. Only the
/// token's SHA-256 hash is stored, never the token itself.
/// <para>
/// Expiry is sliding (<see cref="ExpiresAtUtc"/>, renewed on every rotation) but capped by the
/// family's absolute <see cref="FamilyExpiresAtUtc"/>, so even a constantly used session ends.
/// </para>
/// </summary>
public class RefreshToken : EntityBase<RefreshToken, RefreshTokenId>, IAggregateRoot
{
    /// <summary>A SHA-256 hash, hex-encoded.</summary>
    public const int TokenHashLength = 64;

    private RefreshToken() { } // EF Core

    private RefreshToken(
        UserId userId,
        Guid familyId,
        string tokenHash,
        DateTimeOffset now,
        DateTimeOffset expiresAtUtc,
        DateTimeOffset familyExpiresAtUtc
    )
    {
        if (tokenHash.Length != TokenHashLength)
            throw new ArgumentException(
                $"Token hash must be {TokenHashLength} hex characters",
                nameof(tokenHash)
            );

        Id = RefreshTokenId.New();
        UserId = userId;
        FamilyId = familyId;
        TokenHash = tokenHash;
        CreatedAtUtc = now;
        ExpiresAtUtc = expiresAtUtc;
        FamilyExpiresAtUtc = familyExpiresAtUtc;
    }

    public UserId UserId { get; private set; }

    /// <summary>Shared by every token of one sign-in session.</summary>
    public Guid FamilyId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset FamilyExpiresAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public RefreshTokenRevocationReason? RevocationReason { get; private set; }

    /// <summary>The first token of a new sign-in session.</summary>
    public static RefreshToken StartFamily(
        UserId userId,
        string tokenHash,
        DateTimeOffset now,
        TimeSpan slidingLifetime,
        TimeSpan absoluteLifetime
    )
    {
        var familyExpiresAt = now + absoluteLifetime;
        return new RefreshToken(
            userId,
            Guid.CreateVersion7(),
            tokenHash,
            now,
            Earliest(now + slidingLifetime, familyExpiresAt),
            familyExpiresAt
        );
    }

    public bool IsActive(DateTimeOffset now) => RevokedAtUtc is null && now < ExpiresAtUtc;

    /// <summary>Revokes this token and returns its successor in the same family.</summary>
    public RefreshToken Rotate(string nextTokenHash, DateTimeOffset now, TimeSpan slidingLifetime)
    {
        if (!IsActive(now))
            throw new InvalidOperationException("Only an active refresh token can be rotated.");

        Revoke(now, RefreshTokenRevocationReason.Rotated);
        return new RefreshToken(
            UserId,
            FamilyId,
            nextTokenHash,
            now,
            Earliest(now + slidingLifetime, FamilyExpiresAtUtc),
            FamilyExpiresAtUtc
        );
    }

    /// <summary>Revokes the token; a token that is already revoked keeps its first reason.</summary>
    public void Revoke(DateTimeOffset now, RefreshTokenRevocationReason reason)
    {
        if (RevokedAtUtc is not null)
            return;

        RevokedAtUtc = now;
        RevocationReason = reason;
    }

    /// <summary>
    /// Whether this token was rotated so recently that presenting it again is most likely a
    /// second tab refreshing at the same moment, not a stolen token being replayed.
    /// </summary>
    public bool WasRotatedWithin(TimeSpan gracePeriod, DateTimeOffset now) =>
        RevocationReason == RefreshTokenRevocationReason.Rotated
        && RevokedAtUtc is { } revokedAt
        && now - revokedAt <= gracePeriod;

    private static DateTimeOffset Earliest(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;
}
