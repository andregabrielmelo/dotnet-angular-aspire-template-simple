namespace AppTemplate.Core.Aggregates.UserAggregate.Events;

/// <summary>
/// Represents an event raised when a user's username is updated.
/// </summary>
/// <param name="User">The user whose username was updated.</param>
public sealed record UserNameUpdatedEvent(User User) : DomainEventBase;
