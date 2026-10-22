namespace AppTemplate.UseCases.Authorization;

/// <summary>
/// The authenticated caller, for authorization decisions that depend on the resource being
/// accessed (e.g. "may update their own profile"). Implemented in Web from the access token.
/// </summary>
public interface ICurrentUser
{
    /// <summary>The identity provider's subject (<c>sub</c>), i.e. <c>User.ExternalId</c>.</summary>
    string? ExternalId { get; }

    IReadOnlySet<string> Permissions { get; }

    bool HasPermission(string permission);
}
