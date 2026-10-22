using AppTemplate.UseCases.Users;
using AppTemplate.UseCases.Users.List;

namespace AppTemplate.Infrastructure.Data.Queries;

public class ListUsersQueryService : IListUsersQueryService
{
    // You can use EF, Dapper, SqlClient, etc. for queries
    private readonly ApplicationDatabaseContext _db;

    public ListUsersQueryService(ApplicationDatabaseContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<UserDto>> ListAsync(
        int page,
        int perPage,
        CancellationToken cancellationToken
    )
    {
        var items = await _db
            // EF Core materializes User from this SQL, so it must return every mapped column
            // (snake_case names, see UseSnakeCaseNamingConvention) - not just the projected ones.
            .Users.FromSqlRaw(
                "SELECT id, external_id, name, email, created_at_utc, phone_number_country_code, phone_number_number, phone_number_extension FROM users"
            )
            .OrderBy(c => c.Id)
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .Select(c => new UserDto(c.Id, c.Name, c.PhoneNumber ?? PhoneNumber.Unknown))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        int totalCount = await _db.Users.CountAsync(cancellationToken);
        int totalPages = (int)Math.Ceiling(totalCount / (double)perPage);
        var result = new PagedResult<UserDto>(items, page, perPage, totalCount, totalPages);

        return result;
    }
}
