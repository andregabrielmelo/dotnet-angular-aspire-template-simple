using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AppTemplate.Infrastructure.Data;

/// <summary>
/// Dispatches in-process domain events once <c>SaveChanges</c> has succeeded. The changes are
/// already saved at that point, so a failing handler can't undo them, and it must not fail the
/// request either: a 500 for a write that happened invites the client to repeat it. The
/// failure is logged and the request carries on. Domain events are therefore best-effort: work
/// that must happen reliably needs a transactional outbox, which this template leaves out. See
/// docs/content/design-decisions.md.
/// </summary>
internal sealed partial class EventDispatchInterceptor(
    IDomainEventDispatcher domainEventDispatcher,
    ILogger<EventDispatchInterceptor> logger
) : SaveChangesInterceptor
{
    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default
    )
    {
        if (eventData.Context is ApplicationDatabaseContext context)
        {
            var entitiesWithEvents = context
                .ChangeTracker.Entries<HasDomainEventsBase>()
                .Select(entry => entry.Entity)
                .Where(entity => entity.DomainEvents.Count > 0)
                .ToArray();

            try
            {
                await domainEventDispatcher.DispatchAndClearEvents(entitiesWithEvents);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogDispatchFailed(logger, exception);
            }
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "A domain event handler failed after the changes were saved; the changes stay saved"
    )]
    private static partial void LogDispatchFailed(ILogger logger, Exception exception);
}
