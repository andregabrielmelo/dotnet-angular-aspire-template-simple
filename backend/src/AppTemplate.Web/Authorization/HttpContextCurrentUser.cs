using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.UseCases.Authorization;
using Microsoft.IdentityModel.JsonWebTokens;

namespace AppTemplate.Web.Authorization;

/// <summary><see cref="ICurrentUser"/> read from the current request's access token.</summary>
public sealed class HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private IReadOnlySet<string>? _permissions;

    public UserId? Id =>
        int.TryParse(
            httpContextAccessor.HttpContext?.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,
            out var value
        ) && UserId.TryFrom(value, out var id)
            ? id
            : null;

    public IReadOnlySet<string> Permissions =>
        _permissions ??=
            httpContextAccessor
                .HttpContext?.User.FindAll(AppClaimTypes.Permission)
                .Select(claim => claim.Value)
                .ToHashSet(StringComparer.Ordinal)
            ?? [];

    public bool HasPermission(string permission) => Permissions.Contains(permission);
}
