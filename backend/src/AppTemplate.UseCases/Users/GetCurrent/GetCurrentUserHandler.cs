using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.Aggregates.UserAggregate.Specifications;
using AppTemplate.Core.ValueObjects;

namespace AppTemplate.UseCases.Users.GetCurrent;

public record CurrentUserDto(UserId Id, UserName Name, EmailAddress Email);

/// <summary>
/// The caller's own profile. <c>NotFound</c> when the user was deleted while their access
/// token is still valid (up to its 15-minute lifetime).
/// </summary>
public class GetCurrentUserHandler(IRepository<User> _repository)
    : IQueryHandler<GetCurrentUserQuery, Result<CurrentUserDto>>
{
    public async ValueTask<Result<CurrentUserDto>> Handle(
        GetCurrentUserQuery query,
        CancellationToken cancellationToken
    )
    {
        var user = await _repository.FirstOrDefaultAsync(
            new UserByIdSpecification(query.UserId),
            cancellationToken
        );

        return user is null
            ? Result.NotFound()
            : new CurrentUserDto(user.Id, user.Name, user.Email);
    }
}
