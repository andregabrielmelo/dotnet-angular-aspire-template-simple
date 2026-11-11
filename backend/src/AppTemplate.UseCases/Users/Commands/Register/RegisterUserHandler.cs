using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.Aggregates.UserAggregate.Events;
using AppTemplate.UseCases.Interfaces;
using AppTemplate.UseCases.Users.Handlers;

namespace AppTemplate.UseCases.Users.Commands.Register;

/// <summary>
/// Creates the profile and its account (<see cref="IIdentityService"/>), then announces it:
/// <see cref="SendConfirmationOnUserRegistered"/> emails the confirmation link.
/// </summary>
public class RegisterUserHandler(IIdentityService _identity, IMediator _mediator)
    : ICommandHandler<RegisterUserCommand, Result<UserId>>
{
    public async ValueTask<Result<UserId>> Handle(
        RegisterUserCommand command,
        CancellationToken cancellationToken
    )
    {
        var created = await _identity.CreateUserAsync(
            command.Name,
            command.Email,
            command.Password,
            cancellationToken
        );
        if (!created.IsSuccess)
        {
            return created;
        }

        await _mediator.Publish(
            new UserRegisteredDomainEvent(created.Value, command.Name, command.Email),
            cancellationToken
        );

        return created;
    }
}
