using AppTemplate.Core.Aggregates.RefreshTokenAggregate;
using AppTemplate.Core.Aggregates.RefreshTokenAggregate.Specifications;

namespace AppTemplate.UseCases.Authentication.Logout;

/// <summary>
/// Ends the sign-in session the refresh token belongs to, by revoking its whole family. Always
/// succeeds for an unknown or already revoked token: there is nothing left to sign out.
/// </summary>
public class LogoutHandler(IRepository<RefreshToken> _refreshTokens, TimeProvider _timeProvider)
    : ICommandHandler<LogoutCommand, Result>
{
    public async ValueTask<Result> Handle(
        LogoutCommand command,
        CancellationToken cancellationToken
    )
    {
        var token = await _refreshTokens.FirstOrDefaultAsync(
            new RefreshTokenByHashSpecification(RefreshTokenHash.Of(command.RefreshToken)),
            cancellationToken
        );
        if (token is null)
        {
            return Result.Success();
        }

        var now = _timeProvider.GetUtcNow();
        var family = await _refreshTokens.ListAsync(
            new ActiveRefreshTokensByFamilySpecification(token.FamilyId),
            cancellationToken
        );
        foreach (var member in family)
        {
            member.Revoke(now, RefreshTokenRevocationReason.LoggedOut);
        }

        try
        {
            await _refreshTokens.UpdateRangeAsync(family, cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            // Another tab refreshed this session in the same instant. The client clears its
            // state either way; it can sign out again to revoke the token that refresh issued.
            return Result.Conflict(
                "The session was refreshed while signing out. Sign out again to end it."
            );
        }

        return Result.Success();
    }
}
