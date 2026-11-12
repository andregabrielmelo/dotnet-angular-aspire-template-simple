namespace AppTemplate.Core.Aggregates.UserAggregate.Events;

/// <summary>A new user signed up: their profile and account are saved.</summary>
public sealed record UserRegisteredDomainEvent(UserId UserId, UserName Name, EmailAddress Email)
    : DomainEventBase;
