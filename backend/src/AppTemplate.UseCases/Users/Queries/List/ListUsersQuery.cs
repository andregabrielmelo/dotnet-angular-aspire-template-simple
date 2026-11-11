using AppTemplate.UseCases.Constants;

namespace AppTemplate.UseCases.Users.Queries.List;

public record ListUsersQuery(int? Page = 1, int? PerPage = PaginationConstants.DEFAULT_PAGE_SIZE)
    : IQuery<Result<PagedResult<UserDto>>>;
