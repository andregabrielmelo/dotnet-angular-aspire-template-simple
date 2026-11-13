using AppTemplate.Core.Aggregates.UserAggregate.Events;
using AppTemplate.Core.Interfaces;
using Mediator;

namespace AppTemplate.Core.Aggregates.UserAggregate.Handlers;

/// <summary>
/// An example of a domain event handler: it reacts to <see cref="UserRegisteredDomainEvent"/>,
/// which <c>RegisterUserHandler</c> publishes once the account exists, by logging it. Domain event
/// handlers live in Core, next to their aggregate, and use only Core (here its
/// <see cref="ILoggerAdapter{T}"/> port). It logs the id only: the event also carries the email, which
/// is personal data.
/// </summary>
public class UserRegisteredEventHandler(ILoggerAdapter<UserRegisteredEventHandler> logger)
    : INotificationHandler<UserRegisteredDomainEvent>
{
    public ValueTask Handle(
        UserRegisteredDomainEvent notification,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation("User {UserId} registered", notification.UserId);
        return ValueTask.CompletedTask;
    }
}
