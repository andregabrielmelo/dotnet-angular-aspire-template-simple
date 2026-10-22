using AppTemplate.UseCases.Authorization;

namespace AppTemplate.Web.Authorization;

/// <summary><see cref="ICurrentUser"/> read from the current request's access token.</summary>
public sealed class HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private IReadOnlySet<string>? _permissions;

    public string? ExternalId => httpContextAccessor.HttpContext?.User.FindFirst("sub")?.Value;

    public IReadOnlySet<string> Permissions =>
        _permissions ??=
            httpContextAccessor
                .HttpContext?.User.FindAll(
                    KeycloakPermissionsClaimsTransformation.PermissionClaimType
                )
                .Select(claim => claim.Value)
                .ToHashSet(StringComparer.Ordinal)
            ?? [];

    public bool HasPermission(string permission) => Permissions.Contains(permission);
}
