using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.UseCases.Caching;

namespace AppTemplate.UseCases.Users.Get;

public record GetUserQuery(UserId UserId) : IQuery<Result<UserDto>>, ICacheable
{
    public string CacheKey => $"{nameof(GetUserQuery)}-{UserId.Value}";

    public IReadOnlyCollection<string> CacheTags => [Caching.CacheTags.User(UserId)];
}
