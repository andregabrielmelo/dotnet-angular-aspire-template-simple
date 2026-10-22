using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.Aggregates.UserAggregate.Specifications;
using AppTemplate.Core.ValueObjects;

namespace AppTemplate.UseCases.Users.Get;

public class GetUserHandler(IRepository<User> _repository)
    : IQueryHandler<GetUserQuery, Result<UserDto>>
{
    public async ValueTask<Result<UserDto>> Handle(
        GetUserQuery request,
        CancellationToken cancellationToken
    )
    {
        var user = await _repository.FirstOrDefaultAsync(
            new UserByIdSpecification(request.UserId),
            cancellationToken
        );

        return user is null
            ? Result.NotFound()
            : new UserDto(user.Id, user.Name, user.PhoneNumber ?? PhoneNumber.Unknown);
    }
}
