using AppTemplate.Core.Aggregates.UserAggregate.Events;
using AppTemplate.Core.Interfaces;
using Mediator;

namespace AppTemplate.Core.Aggregates.UserAggregate.Handlers;

public class UserNameUpdatedEventHandler(IAppLogger<UserNameUpdatedEventHandler> logger)
    : INotificationHandler<UserNameUpdatedEvent>
{
    private readonly IAppLogger<UserNameUpdatedEventHandler> _logger = logger;

    // Example of implementation of domain event
    public ValueTask Handle(UserNameUpdatedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "User {UserId}'s name was updated to {NewName}",
            notification.User.Id,
            notification.User.Name.Value
        );
        return ValueTask.CompletedTask;
    }
}
