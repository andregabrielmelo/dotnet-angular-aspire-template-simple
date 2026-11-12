namespace AppTemplate.UseCases.Users.Queries.List;

/// <summary>
/// Represents a service that will actually fetch the necessary data
/// Typically implemented in Infrastructure
/// </summary>
public interface IListUsersQueryService
{
    Task<PagedResult<UserDto>> ListAsync(
        int page,
        int perPage,
        CancellationToken cancellationToken
    );
}
