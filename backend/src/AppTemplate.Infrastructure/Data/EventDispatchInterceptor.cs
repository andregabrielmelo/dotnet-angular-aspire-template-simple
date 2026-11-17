using Microsoft.EntityFrameworkCore;
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
    private readonly IDomainEventDispatcher _domainEventDispatcher = domainEventDispatcher;

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default
    )
    {
        await DispatchAsync(eventData.Context);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// A synchronous <c>SaveChanges</c> dispatches too, blocking on the handlers, so its events
    /// aren't silently left on the entities. The app always saves asynchronously; prefer that.
    /// </summary>
    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        DispatchAsync(eventData.Context).GetAwaiter().GetResult();
        return base.SavedChanges(eventData, result);
    }

    private Task DispatchAsync(DbContext? dbContext)
    {
        if (dbContext is not ApplicationDatabaseContext context)
        {
            return Task.CompletedTask;
        }

        var entitiesWithEvents = context
            .ChangeTracker.Entries<HasDomainEventsBase>()
            .Select(entry => entry.Entity)
            .Where(entity => entity.DomainEvents.Count > 0)
            .ToArray();

        return _domainEventDispatcher.DispatchAndClearEvents(entitiesWithEvents);
    }
}
