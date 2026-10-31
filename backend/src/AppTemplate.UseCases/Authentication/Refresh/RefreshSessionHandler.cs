using AppTemplate.Core.Aggregates.RefreshTokenAggregate;
using AppTemplate.Core.Aggregates.RefreshTokenAggregate.Specifications;
using AppTemplate.Core.Aggregates.UserAggregate;

namespace AppTemplate.UseCases.Authentication.Refresh;

/// <summary>
/// Rotates the refresh token. A token rotated a moment ago is a concurrent refresh (409, retry
/// with the current cookie); one rotated earlier is a replay of a stolen or stale token, so its
/// whole family is revoked. Other sessions of the same user are untouched.
/// </summary>
public class RefreshSessionHandler(
    IRepository<RefreshToken> _refreshTokens,
    IRepository<User> _users,
    SessionFactory _sessions,
    RefreshTokenOptions _options,
    TimeProvider _timeProvider
) : ICommandHandler<RefreshSessionCommand, Result<AuthSession>>
{
    public const string ConcurrentRefreshMessage =
        "The session was refreshed by another request. Retry with the current refresh token.";

    public async ValueTask<Result<AuthSession>> Handle(
        RefreshSessionCommand command,
        CancellationToken cancellationToken
    )
    {
        var now = _timeProvider.GetUtcNow();
        var token = await _refreshTokens.FirstOrDefaultAsync(
            new RefreshTokenByHashSpecification(RefreshTokenHash.Of(command.RefreshToken)),
            cancellationToken
        );

        if (token is null)
        {
            return Result.Unauthorized();
        }

        if (token.WasRotatedWithin(_options.ReuseGracePeriod, now))
        {
            return Result.Conflict(ConcurrentRefreshMessage);
        }

        if (token.RevocationReason == RefreshTokenRevocationReason.Rotated)
        {
            await RevokeFamilyAsync(token.FamilyId, now, cancellationToken);
            return Result.Unauthorized();
        }

        if (!token.IsActive(now))
        {
            return Result.Unauthorized();
        }

        // Deleting a user deletes their refresh tokens too; this covers a delete in between.
        var user = await _users.GetByIdAsync(token.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Unauthorized();
        }

        try
        {
            return await _sessions.RotateAsync(user, token, cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return Result.Conflict(ConcurrentRefreshMessage);
        }
    }

    private async Task RevokeFamilyAsync(
        Guid familyId,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        var family = await _refreshTokens.ListAsync(
            new ActiveRefreshTokensByFamilySpecification(familyId),
            cancellationToken
        );
        foreach (var member in family)
        {
            member.Revoke(now, RefreshTokenRevocationReason.Reused);
        }

        try
        {
            await _refreshTokens.UpdateRangeAsync(family, cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            // A concurrent refresh of the same family rotated a token first. That rotation
            // started from a token that was already revoked, so it was rejected too.
        }
    }
}
