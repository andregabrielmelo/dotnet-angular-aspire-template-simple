using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.Aggregates.UserAggregate.Specifications;

namespace AppTemplate.UseCases.Users.Get;

public class GetUserHandler(IRepository<User> repository)
    : IQueryHandler<GetUserQuery, Result<UserDto>>
{
    public async ValueTask<Result<UserDto>> Handle(
        GetUserQuery request,
        CancellationToken cancellationToken
    )
    {
        var user = await repository.FirstOrDefaultAsync(
            new UserByIdSpecification(request.UserId),
            cancellationToken
        );

        return user is null ? Result.NotFound() : new UserDto(user.Id, user.Name, user.PhoneNumber);
    }
}
