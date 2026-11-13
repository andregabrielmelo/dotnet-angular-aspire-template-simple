using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.UseCases.Caching;

namespace AppTemplate.UseCases.Users.GetCurrent;

/// <summary>
/// The caller's own profile. Cacheable: the endpoint passes the caller's id, so the key already
/// says whose profile it is, and the permissions come from the session, not from this result.
/// </summary>
public record GetCurrentUserQuery(UserId UserId) : IQuery<Result<CurrentUserDto>>, ICacheable
{
    public string CacheKey => $"{nameof(GetCurrentUserQuery)}-{UserId.Value}";

    public IReadOnlyCollection<string> CacheTags => [Caching.CacheTags.User(UserId)];
}
