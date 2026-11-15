namespace AppTemplate.Core.Aggregates.UserAggregate.Events;

/// <summary>
/// Represents an event raised when a user's name is updated. It carries values, not the
/// aggregate, so a handler sees the change as it was, whatever happens to the user afterwards.
/// </summary>
/// <param name="UserId">The identifier of the user whose name was updated.</param>
/// <param name="NewName">The user's new name.</param>
public sealed record UserNameUpdatedEvent(UserId UserId, UserName NewName) : DomainEventBase;
