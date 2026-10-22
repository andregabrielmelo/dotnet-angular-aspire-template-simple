namespace AppTemplate.UseCases.Users.List;

public class ListUsersHandler(IListUsersQueryService query)
    : IQueryHandler<ListUsersQuery, Result<PagedResult<UserDto>>>
{
    private readonly IListUsersQueryService _query = query;

    public async ValueTask<Result<PagedResult<UserDto>>> Handle(
        ListUsersQuery request,
        CancellationToken cancellationToken
    )
    {
        var result = await _query.ListAsync(
            request.Page ?? 1,
            request.PerPage ?? Constants.DEFAULT_PAGE_SIZE,
            cancellationToken
        );

        return Result.Success(result);
    }
}
