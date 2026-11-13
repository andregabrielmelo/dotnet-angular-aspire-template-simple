namespace AppTemplate.SharedKernel;

/// <summary>
/// Publishes domain events through Mediator. Best-effort: by the time events are dispatched the
/// change that raised them is saved, so a failing handler is logged and never fails the caller
/// (a 500 for a write that happened invites the client to repeat it). Work that must happen
/// reliably needs a transactional outbox, which this template leaves out.
/// The one class that logs through <c>ILogger</c> rather than Core's <c>IAppLogger</c>:
/// SharedKernel sits below Core and can't see it.
/// </summary>
public partial class MediatorDomainEventDispatcher(
    IMediator mediator,
    ILogger<MediatorDomainEventDispatcher> logger
) : IDomainEventDispatcher
{
    public async Task DispatchAndClearEvents(IEnumerable<IHasDomainEvents> entitiesWithEvents)
    {
        foreach (var entity in entitiesWithEvents)
        {
            if (entity is not IHasDomainEvents hasDomainEvents)
            {
                LogNotHasDomainEvents(logger, entity.GetType().Name);
            }

            IDomainEvent[] events = entity.DomainEvents.ToArray();
            entity.ClearDomainEvents();

            foreach (var domainEvent in events)
            {
                try
                {
                    await mediator.Publish(domainEvent).ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    LogHandlerFailed(logger, exception, domainEvent.GetType().Name);
                }
            }
        }
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "A handler of {DomainEvent} failed after the changes were saved; the changes stay saved"
    )]
    private static partial void LogHandlerFailed(
        ILogger logger,
        Exception exception,
        string domainEvent
    );

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "{EntityType} does not implement IHasDomainEvents, so its domain events can't be cleared"
    )]
    private static partial void LogNotHasDomainEvents(ILogger logger, string entityType);
}
