namespace AppTemplate.Core.Aggregates.UserAggregate;

/// <summary>
/// A person's profile: what the application knows about them. Their account (email, password,
/// roles, lockout) is ASP.NET Core Identity's, kept in Infrastructure and linked to this profile
/// by its id, so the domain never sees credentials (ADR 013).
/// </summary>
public class User(UserName name) : EntityBase<User, UserId>, IAggregateRoot
{
    public UserName Name { get; private set; } = name;

    public PhoneNumber? PhoneNumber { get; private set; }

    /// <summary>When the row was inserted; set by the database, so it's only known once saved.</summary>
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static User Create(UserName name) => new(name);

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
}
