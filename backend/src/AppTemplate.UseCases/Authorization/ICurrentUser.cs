using AppTemplate.Core.Aggregates.UserAggregate;

namespace AppTemplate.UseCases.Authorization;

/// <summary>
/// The authenticated caller, for authorization decisions that depend on the resource being
/// accessed (e.g. "may update their own profile"). Implemented in Web from the access token.
/// </summary>
public interface ICurrentUser
{
    /// <summary>The caller's user id (the token's <c>sub</c>); <c>null</c> when anonymous.</summary>
    UserId? Id { get; }

    IReadOnlySet<string> Permissions { get; }

    bool HasPermission(string permission);
}
