using AppTemplate.Core.Aggregates.RefreshTokenAggregate;
using AppTemplate.Core.Aggregates.RefreshTokenAggregate.Specifications;

namespace AppTemplate.UseCases.Authentication.StoreRefreshToken;

/// <summary>
/// Stores a newly issued refresh token with the account's current security stamp, and drops the
/// user's expired ones, so the table stays bounded without a cleanup job.
/// </summary>
public class StoreRefreshTokenHandler(
    IRepository<RefreshToken> _refreshTokens,
    IIdentityService _identity,
    TimeProvider _timeProvider
) : ICommandHandler<StoreRefreshTokenCommand, Result>
{
    public async ValueTask<Result> Handle(
        StoreRefreshTokenCommand command,
        CancellationToken cancellationToken
    )
    {
        var account = await _identity.GetAccountAsync(command.UserId, cancellationToken);
        if (account is null)
        {
            return Result.NotFound();
        }

        var expired = await _refreshTokens.ListAsync(
            new ExpiredRefreshTokensByUserSpecification(command.UserId, _timeProvider.GetUtcNow()),
            cancellationToken
        );
        if (expired.Count > 0)
        {
            await _refreshTokens.DeleteRangeAsync(expired, cancellationToken);
        }

        await _refreshTokens.AddAsync(
            RefreshToken.Create(
                command.UserId,
                RefreshTokenHash.Of(command.Token),
                account.SecurityStamp,
                command.ExpiresAt
            ),
            cancellationToken
        );

        return Result.Success();
    }
}
