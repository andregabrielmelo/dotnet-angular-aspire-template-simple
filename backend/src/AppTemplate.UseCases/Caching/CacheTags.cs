using AppTemplate.Core.Aggregates.UserAggregate;

namespace AppTemplate.UseCases.Caching;

/// <summary>
/// Tags group cache entries, so a write can drop everything derived from the data it changed in
/// one <c>HybridCache.RemoveByTagAsync</c> call (ADR 017).
/// </summary>
public static class CacheTags
{
    /// <summary>
    /// Every cached read about one user (by id, as the current user). A write to that user drops
    /// only their entries, never anyone else's.
    /// </summary>
    public static string User(UserId id) => $"users:{id.Value}";

    /// <summary>
    /// Every cached page of the user list. A page depends on every user, so registering,
    /// updating or deleting anyone drops them all.
    /// </summary>
    public const string UserList = "users:list";
}
