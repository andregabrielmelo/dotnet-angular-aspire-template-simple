namespace AppTemplate.Web.Authorization;

/// <summary>Claim names this API writes into its access tokens beyond the registered JWT ones.</summary>
public static class AppClaimTypes
{
    /// <summary>One claim per permission the user's role grants (RolePermissions).</summary>
    public const string Permission = "permission";
}
