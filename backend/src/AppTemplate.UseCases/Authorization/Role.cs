using Ardalis.SmartEnum;

namespace AppTemplate.UseCases.Authorization;

/// <summary>
/// The roles accounts can have, and what each one may do. Accounts hold them as ASP.NET Core
/// Identity roles: the <see cref="SmartEnum{TEnum}.Name"/> is the Identity role name, and the
/// <see cref="SmartEnum{TEnum}.Value"/> its seeded id (the migrations seed
/// <see cref="SmartEnum{TEnum}.List"/>). Identity's claims factory writes the permissions into the
/// session at sign-in and at every security stamp check, so a role change reaches the user within
/// that interval (five minutes). Ownership
/// ("update your own profile") is not a permission: handlers check it against
/// <c>ICurrentUser.Id</c>.
/// </summary>
public sealed class Role : SmartEnum<Role>
{
    /// <summary>May manage every user.</summary>
    public static readonly Role Admin = new(nameof(Admin), 1, [.. Permission.List]);

    /// <summary>A registered user: may read and update their own profile. Every new account.</summary>
    public static readonly Role Member = new(nameof(Member), 2, []);

    private Role(string name, int value, IReadOnlyList<Permission> permissions)
        : base(name, value) => Permissions = permissions;

    /// <summary>What this role may do.</summary>
    public IReadOnlyList<Permission> Permissions { get; }

    /// <summary>
    /// The permissions an account's Identity roles grant, without duplicates, ordered by their
    /// claim. A role name not defined here (added in the database only) grants nothing.
    /// </summary>
    public static IReadOnlyList<Permission> PermissionsFor(IEnumerable<string> roleNames) =>
        roleNames
            .Select(name => TryFromName(name, out var role) ? role : null)
            .OfType<Role>()
            .SelectMany(role => role.Permissions)
            .Distinct()
            .OrderBy(permission => permission.Value, StringComparer.Ordinal)
            .ToList();
}
