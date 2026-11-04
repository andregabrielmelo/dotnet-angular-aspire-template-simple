using AppTemplate.Core.Aggregates.RefreshTokenAggregate;
using AppTemplate.Core.Aggregates.RefreshTokenAggregate.Specifications;

namespace AppTemplate.UseCases.Authentication.Logout;

/// <summary>
/// Ends the session by deleting its refresh token. Succeeds when there is nothing to delete:
/// there is nothing left to sign out. The access token is revoked by the endpoint (Web).
/// </summary>
public class LogoutHandler(IRepository<RefreshToken> _refreshTokens)
    : ICommandHandler<LogoutCommand, Result>
{
    public async ValueTask<Result> Handle(
        LogoutCommand command,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrEmpty(command.RefreshToken))
        {
            return Result.Success();
        }

        var token = await _refreshTokens.FirstOrDefaultAsync(
            new RefreshTokenByHashSpecification(RefreshTokenHash.Of(command.RefreshToken)),
            cancellationToken
        );
        if (token is null)
        {
            return Result.Success();
        }

        try
        {
            await _refreshTokens.DeleteAsync(token, cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            // A refresh used it in the meantime; it is gone either way.
        }

        return Result.Success();
    }
}
