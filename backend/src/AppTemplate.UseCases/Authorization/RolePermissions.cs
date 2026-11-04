namespace AppTemplate.UseCases.Authorization;

/// <summary>
/// The permissions each role grants. They are written into the access token as
/// <c>permissions</c> claims when it's issued, so a role change reaches the user with their next
/// access token. Ownership ("update your own profile") is not a permission: handlers check it
/// against <see cref="ICurrentUser.Id"/>.
/// </summary>
public static class RolePermissions
{
    public static IReadOnlyList<string> For(IEnumerable<string> roles) =>
        roles.SelectMany(For).Distinct().Order(StringComparer.Ordinal).ToList();

    private static IReadOnlyList<string> For(string role) =>
        role switch
        {
            Roles.Admin => Permission.All,
            Roles.Member => [],
            // A role added in the database but not here grants nothing.
            _ => [],
        };
}
