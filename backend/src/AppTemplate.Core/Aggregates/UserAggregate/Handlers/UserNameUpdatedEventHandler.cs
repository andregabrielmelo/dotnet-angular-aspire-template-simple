using AppTemplate.Core.Aggregates.UserAggregate.Events;
using AppTemplate.Core.Interfaces;
using Mediator;

namespace AppTemplate.Core.Aggregates.UserAggregate.Handlers;

/// <summary>
/// Handles user name updated events.
/// </summary>
/// <param name="logger">The logger used to record user name updates.</param>
public class UserNameUpdatedEventHandler(ILoggerAdapter<UserNameUpdatedEventHandler> logger)
    : INotificationHandler<UserNameUpdatedEvent>
{
    private readonly ILoggerAdapter<UserNameUpdatedEventHandler> _logger = logger;

    // Example of implementation of domain event
    public ValueTask Handle(UserNameUpdatedEvent notification, CancellationToken cancellationToken)
    {
        // The id only: the new name is personal data, which never goes into logs.
        _logger.LogInformation("User {UserId}'s name was updated", notification.UserId);
        return ValueTask.CompletedTask;
    }
}
