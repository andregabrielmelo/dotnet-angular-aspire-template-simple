using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AppTemplate.Infrastructure.Data;

/// <summary>
/// Dispatches in-process domain events once <c>SaveChanges</c> has succeeded. The changes are
/// already saved at that point, so a failing handler can't undo them; the dispatcher logs the
/// failure and the request carries on (see <see cref="MediatorDomainEventDispatcher"/>). Domain
/// events are therefore best-effort: work that must happen reliably needs a transactional
/// outbox, which this template leaves out. See docs/content/design-decisions.md.
/// </summary>
internal sealed class EventDispatchInterceptor(IDomainEventDispatcher domainEventDispatcher)
    : SaveChangesInterceptor
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

            await domainEventDispatcher.DispatchAndClearEvents(entitiesWithEvents);
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }
}
