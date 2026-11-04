using AppTemplate.Core.Aggregates.UserAggregate.Events;

namespace AppTemplate.Core.Aggregates.UserAggregate;

/// <summary>
/// The application's own view of a person, including the hash of the password they sign in
/// with. The domain never sees the password itself or how it's hashed: use cases hash it
/// through <c>IPasswordHasher</c>.
/// </summary>
public class User(UserName name, EmailAddress email, string passwordHash, UserRole role)
    : EntityBase<User, UserId>,
        IAggregateRoot
{
    public const int PasswordHashMaxLength = 256;

    public UserName Name { get; private set; } = name;
    public EmailAddress Email { get; private set; } = email;
    public string PasswordHash { get; private set; } = passwordHash;

    /// <summary>What the user may do; use cases map it to permissions (<c>RolePermissions</c>).</summary>
    public UserRole Role { get; private set; } = role;

    public PhoneNumber? PhoneNumber { get; private set; }

    /// <summary>When the row was inserted; set by the database, so it's only known once saved.</summary>
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static User Create(
        UserName name,
        EmailAddress email,
        string passwordHash,
        UserRole role = UserRole.Member
    )
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password hash cannot be empty", nameof(passwordHash));

        if (passwordHash.Length > PasswordHashMaxLength)
            throw new ArgumentException(
                $"Password hash must not exceed {PasswordHashMaxLength} characters",
                nameof(passwordHash)
            );

        return new User(name, email, passwordHash, role);
    }

    public User UpdateName(UserName newName)
    {
        if (Name == newName)
        {
            return this;
        }

        Name = newName;
        return this;
    }

    public User UpdatePhoneNumber(PhoneNumber newPhoneNumber)
    {
        PhoneNumber = newPhoneNumber;
        return this;
    }

    /// <summary>Takes effect in the user's next access token (at the latest on the next refresh).</summary>
    public User ChangeRole(UserRole newRole)
    {
        Role = newRole;
        return this;
    }
}
