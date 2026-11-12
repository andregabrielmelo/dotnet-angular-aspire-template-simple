namespace AppTemplate.UseCases.Authorization;

/// <summary>
/// The claims the session cookie carries beyond Identity's own (account id, email, roles, security
/// stamp). Infrastructure's claims factory writes them; Web reads them.
/// </summary>
public static class AppClaimTypes
{
    /// <summary>The profile's <c>UserId</c>, which the rest of the application uses.</summary>
    public const string UserId = "user_id";

    /// <summary>
    /// One claim per permission the user's roles grant (<see cref="Role.PermissionsFor"/>).
    /// FastEndpoints' default permissions claim type, which <c>Permissions(...)</c> checks.
    /// </summary>
    public const string Permission = "permissions";
}
