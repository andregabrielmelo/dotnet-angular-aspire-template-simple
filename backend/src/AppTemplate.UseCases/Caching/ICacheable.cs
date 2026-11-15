namespace AppTemplate.UseCases.Caching;

/// <summary>
/// A query whose response the Mediator pipeline caches in HybridCache (Web's
/// <c>CachingBehavior</c>, ADR 017). Only successful results are cached; a failure such as
/// <c>NotFound</c> always reaches the handler. Lifetimes come from its <see cref="CacheProfile"/>.
/// <para>
/// On a hit the handler doesn't run. Mark a query cacheable only when its result depends on
/// nothing but <see cref="CacheKey"/>, and its handler does no per-caller authorization: the
/// endpoint's own authorization still runs first (FastEndpoints authorizes before Mediator), but
/// a <c>Forbidden</c> decided inside the handler would be skipped. Every write that changes the
/// result must remove one of <see cref="CacheTags"/>.
/// </para>
/// </summary>
public interface ICacheable
{
    /// <summary>Unique per result: built from everything the result depends on.</summary>
    string CacheKey { get; }

    /// <summary>What writes invalidate, such as <see cref="Caching.CacheTags.User"/>.</summary>
    /// <remarks>
    /// Writes remove tags rather than keys because one write can change many cached results: a
    /// registration changes every page of the user list (<see cref="Caching.CacheTags.UserList"/>),
    /// whose keys include the page and size, and an update changes both the user's own reads and
    /// those pages. A tag reaches all of them without the write knowing their keys.
    /// </remarks>
    IReadOnlyCollection<string> CacheTags { get; }

    /// <summary>How long the result lives; override it only to pick another profile.</summary>
    CacheProfile CacheProfile => CacheProfile.None;
}
