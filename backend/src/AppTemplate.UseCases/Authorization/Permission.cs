using Ardalis.SmartEnum;

namespace AppTemplate.UseCases.Authorization;

/// <summary>
/// Fine-grained permissions the API enforces. Roles grant them (<see cref="Role.Permissions"/>),
/// and they travel in the session cookie as <c>permissions</c> claims, which FastEndpoints'
/// <c>Permissions(...)</c> checks on an endpoint. The <see cref="SmartEnum{TEnum, TValue}.Value"/>
/// is the claim (<c>users:read</c>); a permission converts to it implicitly, so it can be passed
/// wherever a string is expected. Its <c>ToString()</c> is the C# name, so write <c>.Value</c> in
/// text.
/// </summary>
public sealed class Permission : SmartEnum<Permission, string>
{
    /// <summary>List users and read any user's profile.</summary>
    public static readonly Permission UsersRead = new(nameof(UsersRead), "users:read");

    /// <summary>Update any user's profile (everyone may update their own).</summary>
    public static readonly Permission UsersWrite = new(nameof(UsersWrite), "users:write");

    /// <summary>Delete users.</summary>
    public static readonly Permission UsersDelete = new(nameof(UsersDelete), "users:delete");

    private Permission(string name, string value)
        : base(name, value) { }
}
