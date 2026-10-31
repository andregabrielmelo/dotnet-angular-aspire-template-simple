using AppTemplate.Core.Aggregates.UserAggregate;

namespace AppTemplate.UseCases.Authorization;

/// <summary>
/// The permissions each role grants. They are written into the access token as
/// <c>permission</c> claims when it's issued, so a role change reaches the user with their next
/// access token. Ownership ("update your own profile") is not a permission: handlers check it
/// against <see cref="ICurrentUser.Id"/>.
/// </summary>
public static class RolePermissions
{
    public static IReadOnlyList<string> For(UserRole role) =>
        role switch
        {
            UserRole.Admin => Permission.All,
            UserRole.Member => [],
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown role"),
        };
}
