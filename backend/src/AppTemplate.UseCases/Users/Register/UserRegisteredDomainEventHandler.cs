using AppTemplate.Core.Aggregates.UserAggregate.Events;

namespace AppTemplate.UseCases.Users.Register;

/// <summary>
/// Welcomes a new user. Best-effort, like every domain event handler: the email is lost if
/// sending fails or the process stops after the user was saved. Make it reliable with a
/// transactional outbox when it matters (see docs/content/design-decisions.md).
/// </summary>
public sealed class UserRegisteredDomainEventHandler(IEmailSender _emailSender)
    : IDomainEventHandler<UserRegisteredDomainEvent>
{
    public async ValueTask Handle(
        UserRegisteredDomainEvent domainEvent,
        CancellationToken cancellationToken
    )
    {
        await _emailSender.SendAsync(
            domainEvent.Email,
            "Welcome!",
            "Thanks for registering. We're glad to have you on board.",
            cancellationToken
        );
    }
}
