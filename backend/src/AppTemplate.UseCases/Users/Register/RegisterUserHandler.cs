using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.Aggregates.UserAggregate.Events;
using AppTemplate.Core.Aggregates.UserAggregate.Specifications;
using AppTemplate.UseCases.Authentication;

namespace AppTemplate.UseCases.Users.Register;

public class RegisterUserHandler(
    IRepository<User> _users,
    IPasswordHasher _passwordHasher,
    IMediator _mediator
) : ICommandHandler<RegisterUserCommand, Result<AuthenticatedUserDto>>
{
    public async ValueTask<Result<AuthenticatedUserDto>> Handle(
        RegisterUserCommand command,
        CancellationToken cancellationToken
    )
    {
        if (await _users.AnyAsync(new UserByEmailSpecification(command.Email), cancellationToken))
        {
            return Result.Conflict("An account with this email already exists");
        }

        var user = User.Create(command.Name, command.Email, _passwordHasher.Hash(command.Password));
        await _users.AddAsync(user, cancellationToken);

        var domainEvent = new UserRegisteredDomainEvent(user.Id, user.Email.ToString());
        await _mediator.Publish(domainEvent, cancellationToken);

        return AuthenticatedUserDto.FromEntity(user);
    }
}
