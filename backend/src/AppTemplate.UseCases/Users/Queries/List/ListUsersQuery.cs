using AppTemplate.UseCases.Caching;
using AppTemplate.UseCases.Constants;

namespace AppTemplate.UseCases.Users.Queries.List;

/// <summary>
/// A page of users. Cacheable: the page is the same for everyone the endpoint lets in
/// (<c>users:read</c>), keyed by the page and its size. Any user write drops every cached page
/// (<see cref="CacheTags.UserList"/>).
/// </summary>
public record ListUsersQuery(int? Page = 1, int? PerPage = PaginationConstants.DEFAULT_PAGE_SIZE)
    : IQuery<Result<PagedResult<UserDto>>>,
        ICacheable
{
    /// <summary>The page asked for, or the first.</summary>
    public int EffectivePage => Page ?? 1;

    /// <summary>The page size asked for, or the default.</summary>
    public int EffectivePerPage => PerPage ?? PaginationConstants.DEFAULT_PAGE_SIZE;

    public string CacheKey => $"{nameof(ListUsersQuery)}-{EffectivePage}-{EffectivePerPage}";

    public IReadOnlyCollection<string> CacheTags => [Caching.CacheTags.UserList];
}
