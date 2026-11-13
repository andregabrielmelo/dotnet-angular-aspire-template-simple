using AppTemplate.Core.Aggregates.UserAggregate.Events;
using Mediator;
using Microsoft.Extensions.Logging;

namespace AppTemplate.Core.Aggregates.UserAggregate.Handlers;

public class UserNameUpdatedEventHandler(ILogger<UserNameUpdatedEventHandler> logger)
    : INotificationHandler<UserNameUpdatedEvent>
{
    private readonly ILogger<UserNameUpdatedEventHandler> _logger = logger;

    // Example of implementation of domain event
    public ValueTask Handle(UserNameUpdatedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("User {UserId}'s name was updated to {NewName}", notification.User.Id, notification.User.Name);
        return ValueTask.CompletedTask;
    }
}
