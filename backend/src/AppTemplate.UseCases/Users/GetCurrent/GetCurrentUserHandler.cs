using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.Aggregates.UserAggregate.Specifications;
using AppTemplate.Core.ValueObjects;
using AppTemplate.UseCases.Authentication;

namespace AppTemplate.UseCases.Users.GetCurrent;

public record CurrentUserDto(UserId Id, UserName Name, EmailAddress Email);

/// <summary>
/// The caller's own profile, with their account's email. <c>NotFound</c> when the user was
/// deleted while their access token is still valid (up to its 15-minute lifetime).
/// </summary>
public class GetCurrentUserHandler(IRepository<User> _repository, IIdentityService _identity)
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

        var account = user is null
            ? null
            : await _identity.GetAccountAsync(user.Id, cancellationToken);

        return user is null || account is null
            ? Result.NotFound()
            : new CurrentUserDto(user.Id, user.Name, account.Email);
    }
}
