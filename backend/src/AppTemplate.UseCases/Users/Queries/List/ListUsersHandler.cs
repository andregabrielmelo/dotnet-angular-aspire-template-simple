namespace AppTemplate.UseCases.Users.Queries.List;

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
            request.EffectivePage,
            request.EffectivePerPage,
            cancellationToken
        );

        return Result.Success(result);
    }
}
