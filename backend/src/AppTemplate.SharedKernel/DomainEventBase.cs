namespace AppTemplate.SharedKernel;

/// <summary>
/// A base type for domain events. Depends on Mediator INotification.
/// Includes DateOccurred which is set on creation. A record, so events can be positional:
/// <c>public sealed record UserRegisteredDomainEvent(UserId UserId, string Email) : DomainEventBase;</c>
/// </summary>
public abstract record DomainEventBase : IDomainEvent
{
    public DateTime DateOccurred { get; protected init; } = DateTime.UtcNow;
}
