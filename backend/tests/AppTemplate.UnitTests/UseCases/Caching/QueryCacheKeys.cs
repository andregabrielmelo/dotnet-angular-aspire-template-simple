using AppTemplate.UseCases.Caching;
using AppTemplate.UseCases.Constants;
using AppTemplate.UseCases.Users.Get;
using AppTemplate.UseCases.Users.GetCurrent;
using AppTemplate.UseCases.Users.List;

namespace AppTemplate.UnitTests.UseCases.Caching;

/// <summary>Cache keys are the query's name and everything its result depends on.</summary>
public class QueryCacheKeys
{
    [Fact]
    public void NamesAUserByQueryAndId()
    {
        new GetUserQuery(UserId.From(5)).CacheKey.ShouldBe("GetUserQuery-5");
        new GetCurrentUserQuery(UserId.From(5)).CacheKey.ShouldBe("GetCurrentUserQuery-5");
    }

    [Fact]
    public void TagsAUsersReadsWithTheirOwnTag()
    {
        new GetUserQuery(UserId.From(5)).CacheTags.ShouldBe(["users:5"]);
        new GetCurrentUserQuery(UserId.From(5)).CacheTags.ShouldBe(["users:5"]);
    }

    [Fact]
    public void NamesAListPageByQueryPageAndSize()
    {
        var query = new ListUsersQuery(2, 50);

        query.CacheKey.ShouldBe("ListUsersQuery-2-50");
        query.CacheTags.ShouldBe([CacheTags.UserList]);
    }

    [Fact]
    public void GivesAnOmittedPageAndSizeTheSameKeyAsTheirDefaults()
    {
        new ListUsersQuery(null, null).CacheKey.ShouldBe(
            new ListUsersQuery(1, PaginationConstants.DEFAULT_PAGE_SIZE).CacheKey
        );
    }
}
