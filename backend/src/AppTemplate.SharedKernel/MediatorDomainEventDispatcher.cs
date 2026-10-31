namespace AppTemplate.SharedKernel;

/// <summary>
/// Publishes domain events through Mediator. Best-effort: by the time events are dispatched the
/// change that raised them is saved, so a failing handler is logged and never fails the caller
/// (a 500 for a write that happened invites the client to repeat it). Work that must happen
/// reliably needs a transactional outbox, which this template leaves out.
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
}
