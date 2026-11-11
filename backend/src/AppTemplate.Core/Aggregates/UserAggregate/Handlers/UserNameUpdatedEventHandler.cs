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
        var userId = notification.User.Id;
        string newName = notification.User.Name.Value;
        _logger.LogInformation("User {userId}'s name was updated to {newName}", userId, newName);
        return ValueTask.CompletedTask;
    }
}
