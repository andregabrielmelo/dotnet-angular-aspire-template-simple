using AppTemplate.Core.Aggregates.RefreshTokenAggregate;
using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.UseCases.Authorization;

namespace AppTemplate.UseCases.Authentication;

/// <summary>
/// The one place a session is built, for sign-in, registration and refresh alike: the user's
/// current permissions go into a new access token, and a new refresh token is stored as its
/// hash. Each call saves once, so a refresh token is never half-persisted.
/// </summary>
public sealed class SessionFactory(
    IRepository<RefreshToken> refreshTokens,
    ITokenIssuer tokenIssuer,
    RefreshTokenOptions options,
    TimeProvider timeProvider
)
{
    /// <summary>A new sign-in: the first token of a new family.</summary>
    public async Task<AuthSession> StartAsync(User user, CancellationToken cancellationToken)
    {
        var value = tokenIssuer.CreateRefreshTokenValue();
        var token = RefreshToken.StartFamily(
            user.Id,
            RefreshTokenHash.Of(value),
            timeProvider.GetUtcNow(),
            options.SlidingLifetime,
            options.AbsoluteLifetime
        );

        await refreshTokens.AddAsync(token, cancellationToken);

        return Issue(user, value, token);
    }

    /// <summary>
    /// Exchanges <paramref name="current"/> for the next token of its family. Throws
    /// <see cref="ConcurrencyConflictException"/> when another request rotated it first.
    /// </summary>
    public async Task<AuthSession> RotateAsync(
        User user,
        RefreshToken current,
        CancellationToken cancellationToken
    )
    {
        var value = tokenIssuer.CreateRefreshTokenValue();
        var next = current.Rotate(
            RefreshTokenHash.Of(value),
            timeProvider.GetUtcNow(),
            options.SlidingLifetime
        );

        // One SaveChanges, so one transaction: `current` is tracked, so adding `next` also saves
        // its revocation. The row version check on `current` makes that revocation conditional:
        // when a concurrent refresh revoked it first, nothing is saved and this throws.
        await refreshTokens.AddAsync(next, cancellationToken);

        return Issue(user, value, next);
    }

    private AuthSession Issue(User user, string refreshTokenValue, RefreshToken refreshToken)
    {
        var accessToken = tokenIssuer.IssueAccessToken(user, RolePermissions.For(user.Role));
        return new AuthSession(
            accessToken.Value,
            accessToken.ExpiresAt,
            refreshTokenValue,
            refreshToken.ExpiresAtUtc
        );
    }
}
