namespace AppTemplate.UseCases.Authorization;

/// <summary>
/// The roles accounts can have, stored as ASP.NET Core Identity roles (seeded by the
/// migrations). <see cref="RolePermissions"/> says what each one may do.
/// </summary>
public static class Roles
{
    /// <summary>May manage every user.</summary>
    public const string Admin = "Admin";

    /// <summary>A registered user: may read and update their own profile. Every new account.</summary>
    public const string Member = "Member";

    public static IReadOnlyList<string> All { get; } = [Admin, Member];
}
