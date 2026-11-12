namespace AppTemplate.Core.Aggregates.UserAggregate.Events;

public sealed record UserNameUpdatedEvent(User User) : DomainEventBase;
