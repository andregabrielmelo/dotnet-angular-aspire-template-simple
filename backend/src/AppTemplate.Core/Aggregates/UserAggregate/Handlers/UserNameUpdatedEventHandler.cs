using AppTemplate.Core.Aggregates.UserAggregate.Events;
using Mediator;
using Microsoft.Extensions.Logging;

namespace AppTemplate.Core.Aggregates.UserAggregate.Handlers;

public partial class UserNameUpdatedEventHandler(ILogger<UserNameUpdatedEventHandler> logger)
    : INotificationHandler<UserNameUpdatedEvent>
{
    private readonly ILogger<UserNameUpdatedEventHandler> _logger = logger;

    // Example of implementation of domain event
    public ValueTask Handle(UserNameUpdatedEvent notification, CancellationToken cancellationToken)
    {
        LogNameUpdated(_logger, notification.User.Id, notification.User.Name.Value);
        return ValueTask.CompletedTask;
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "User {UserId}'s name was updated to {NewName}"
    )]
    private static partial void LogNameUpdated(ILogger logger, UserId userId, string newName);
}
