using AppTemplate.Core.Aggregates.UserAggregate;

namespace AppTemplate.Core.Aggregates.RefreshTokenAggregate.Specifications;

/// <summary>A user's refresh tokens that can no longer be used, tracked so they can be deleted.</summary>
public class ExpiredRefreshTokensByUserSpecification : Specification<RefreshToken>
{
    public ExpiredRefreshTokensByUserSpecification(UserId userId, DateTimeOffset now) =>
        Query.Where(token => token.UserId == userId && token.ExpiresAtUtc <= now);
}
