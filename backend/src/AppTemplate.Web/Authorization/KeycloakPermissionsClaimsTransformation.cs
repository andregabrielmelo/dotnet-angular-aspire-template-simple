using System.Security.Claims;
using System.Text.Json;
using AppTemplate.UseCases.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace AppTemplate.Web.Authorization;

public sealed class KeycloakAuthorizationOptions
{
    /// <summary>
    /// The Keycloak client whose client roles are this API's permissions. Set from the
    /// required <c>Keycloak:Audience</c> (see <c>AuthorizationConfigurations</c>).
    /// </summary>
    public string ApiClientId { get; set; } = string.Empty;
}

/// <summary>
/// Keycloak puts client roles in the access token as
/// <c>"resource_access": { "apptemplate-api": { "roles": ["users:read"] } }</c>. This flattens
/// this API's roles into <see cref="PermissionClaimType"/> claims that authorization policies
/// check. Only permissions the code knows (<see cref="Permission.All"/>) are mapped.
/// </summary>
public sealed class KeycloakPermissionsClaimsTransformation(
    IOptions<KeycloakAuthorizationOptions> options
) : IClaimsTransformation
{
    public const string PermissionClaimType = "permission";
    private const string AuthenticationType = "keycloak-permissions";

    private static readonly HashSet<string> KnownPermissions = new(
        Permission.All,
        StringComparer.Ordinal
    );

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        // Transformations can run more than once per request - only add permissions once.
        if (
            principal.Identity?.IsAuthenticated != true
            || principal.Identities.Any(i => i.AuthenticationType == AuthenticationType)
        )
        {
            return Task.FromResult(principal);
        }

        var permissions = ReadClientRoles(principal, options.Value.ApiClientId)
            .Where(KnownPermissions.Contains)
            .Distinct(StringComparer.Ordinal)
            .Select(permission => new Claim(PermissionClaimType, permission));

        // Add a separate identity rather than mutating the one the handler produced.
        var transformed = principal.Clone();
        transformed.AddIdentity(new ClaimsIdentity(permissions, AuthenticationType));
        return Task.FromResult(transformed);
    }

    private static IEnumerable<string> ReadClientRoles(ClaimsPrincipal principal, string clientId)
    {
        var resourceAccess = principal.FindFirst("resource_access")?.Value;
        if (string.IsNullOrEmpty(resourceAccess))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(resourceAccess);
            if (
                document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty(clientId, out var client)
                && client.ValueKind == JsonValueKind.Object
                && client.TryGetProperty("roles", out var roles)
                && roles.ValueKind == JsonValueKind.Array
            )
            {
                return roles
                    .EnumerateArray()
                    .Where(role => role.ValueKind == JsonValueKind.String)
                    .Select(role => role.GetString()!)
                    .ToList();
            }
        }
        catch (JsonException)
        {
            // A malformed claim grants nothing.
        }

        return [];
    }
}
