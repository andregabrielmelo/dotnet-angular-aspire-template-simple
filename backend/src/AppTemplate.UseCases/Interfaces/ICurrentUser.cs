using AppTemplate.Core.Aggregates.UserAggregate;

namespace AppTemplate.UseCases.Interfaces;

/// <summary>
/// The authenticated caller, for authorization decisions that depend on the resource being
/// accessed (e.g. "may update their own profile"). Implemented in Web from the session's claims.
/// </summary>
public interface ICurrentUser
{
    /// <summary>The caller's profile id (the session's <c>user_id</c> claim); <c>null</c> when anonymous.</summary>
    UserId? Id { get; }

    IReadOnlySet<string> Permissions { get; }

    bool HasPermission(string permission);
}
