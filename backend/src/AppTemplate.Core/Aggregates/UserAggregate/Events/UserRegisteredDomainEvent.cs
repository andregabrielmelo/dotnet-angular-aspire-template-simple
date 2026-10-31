namespace AppTemplate.Core.Aggregates.UserAggregate.Events;

/// <summary>A new user signed up. Dispatched in-process once the user is saved.</summary>
public sealed record UserRegisteredDomainEvent(UserId UserId, string Email) : DomainEventBase;
