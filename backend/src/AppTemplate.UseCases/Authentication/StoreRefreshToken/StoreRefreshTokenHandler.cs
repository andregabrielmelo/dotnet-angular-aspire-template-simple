using AppTemplate.Core.Aggregates.RefreshTokenAggregate;
using AppTemplate.Core.Aggregates.RefreshTokenAggregate.Specifications;

namespace AppTemplate.UseCases.Authentication.StoreRefreshToken;

/// <summary>
/// Stores a newly issued refresh token, and drops the user's expired ones, so the table stays
/// bounded without a cleanup job.
/// </summary>
public class StoreRefreshTokenHandler(
    IRepository<RefreshToken> _refreshTokens,
    TimeProvider _timeProvider
) : ICommandHandler<StoreRefreshTokenCommand, Result>
{
    public async ValueTask<Result> Handle(
        StoreRefreshTokenCommand command,
        CancellationToken cancellationToken
    )
    {
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
                command.ExpiresAt
            ),
            cancellationToken
        );

        return Result.Success();
    }
}
