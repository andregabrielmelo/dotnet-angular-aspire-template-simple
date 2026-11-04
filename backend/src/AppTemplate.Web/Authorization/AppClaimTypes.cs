namespace AppTemplate.Web.Authorization;

/// <summary>Claim names this API writes into its access tokens beyond the registered JWT ones.</summary>
public static class AppClaimTypes
{
    /// <summary>
    /// One claim per permission the user's role grants (RolePermissions). FastEndpoints' default
    /// permissions claim type: <c>JwtBearer.CreateToken</c> writes it, and <c>Permissions(...)</c> checks it.
    /// </summary>
    public const string Permission = "permissions";
}
