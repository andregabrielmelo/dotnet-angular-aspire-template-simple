namespace AppTemplate.Core.Aggregates.UserAggregate.Events;

/// <summary>
/// Represents an event raised when a user is registered.
/// </summary>
/// <param name="UserId">The identifier of the registered user.</param>
/// <param name="Name">The name of the registered user.</param>
/// <param name="Email">The email address of the registered user.</param>
public sealed record UserRegisteredDomainEvent(UserId UserId, UserName Name, EmailAddress Email)
    : DomainEventBase;
