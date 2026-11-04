using AppTemplate.Core.Aggregates.RefreshTokenAggregate;
using AppTemplate.Core.Aggregates.RefreshTokenAggregate.Specifications;
using AppTemplate.Core.Aggregates.UserAggregate;

namespace AppTemplate.UseCases.Authentication.ConsumeRefreshToken;

/// <summary>
/// Uses up a refresh token: it is deleted, so each one works once, and returns the user it belongs
/// to with their current roles. Of two requests using the same token at once, the second delete
/// finds no row, so only one of them gets new tokens. Other sessions of the user are untouched,
/// unless the account's password changed since the token was issued (its security stamp differs)
/// or the account is locked out: then no session refreshes.
/// </summary>
public class ConsumeRefreshTokenHandler(
    IRepository<RefreshToken> _refreshTokens,
    IRepository<User> _users,
    IIdentityService _identity,
    TimeProvider _timeProvider
) : ICommandHandler<ConsumeRefreshTokenCommand, Result<AuthenticatedUserDto>>
{
    public async ValueTask<Result<AuthenticatedUserDto>> Handle(
        ConsumeRefreshTokenCommand command,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrEmpty(command.Token))
        {
            return Result.Unauthorized();
        }

        var token = await _refreshTokens.FirstOrDefaultAsync(
            new RefreshTokenByHashSpecification(RefreshTokenHash.Of(command.Token)),
            cancellationToken
        );
        if (token is null)
        {
            return Result.Unauthorized();
        }

        try
        {
            await _refreshTokens.DeleteAsync(token, cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            // Another request used this token first.
            return Result.Unauthorized();
        }

        if (token.IsExpired(_timeProvider.GetUtcNow()))
        {
            return Result.Unauthorized();
        }

        // Deleting a user deletes their refresh tokens too; this covers a delete in between.
        var account = await _identity.GetAccountAsync(token.UserId, cancellationToken);
        if (account is null || account.IsLockedOut || token.IsStale(account.SecurityStamp))
        {
            return Result.Unauthorized();
        }

        var profile = await _users.GetByIdAsync(token.UserId, cancellationToken);
        return profile is null
            ? Result.Unauthorized()
            : AuthenticatedUserDto.From(profile, account);
    }
}
