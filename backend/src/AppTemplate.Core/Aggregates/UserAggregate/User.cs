using AppTemplate.Core.Aggregates.UserAggregate.Events;

namespace AppTemplate.Core.Aggregates.UserAggregate;

/// <summary>
/// The application's own view of a person. Credentials live in the OpenID Connect provider
/// (Keycloak), not here - <see cref="ExternalId"/> links this row to that identity via its
/// <c>sub</c> claim.
/// </summary>
public class User(string externalId, UserName name, EmailAddress email)
    : EntityBase<User, UserId>,
        IAggregateRoot,
        IAuditable
{
    public const int ExternalIdMaxLength = 64;

    public string ExternalId { get; private set; } = externalId;
    public UserName Name { get; private set; } = name;
    public EmailAddress Email { get; private set; } = email;
    public PhoneNumber? PhoneNumber { get; private set; }

    /// <summary>
    /// When the welcome email went out. Background jobs can run more than once (retries), so
    /// the sender checks this first to keep the email from being sent twice.
    /// </summary>
    public DateTimeOffset? WelcomeEmailSentAtUtc { get; private set; }

    /// <summary>When the row was inserted; set by the database, so it's only known once saved.</summary>
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static User Create(string externalId, UserName name, EmailAddress email)
    {
        if (string.IsNullOrWhiteSpace(externalId))
            throw new ArgumentException("External id cannot be empty", nameof(externalId));

        if (externalId.Length > ExternalIdMaxLength)
            throw new ArgumentException(
                $"External id must not exceed {ExternalIdMaxLength} characters",
                nameof(externalId)
            );

        var user = new User(externalId, name, email);
        // Saved to the outbox with the user, so the welcome email can't be lost (see
        // SendWelcomeEmailWhenUserProvisioned).
        user.RaiseIntegrationEvent(new UserProvisioned(externalId));
        return user;
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

    /// <summary>Keeps the profile in step with the identity provider, where users change it.</summary>
    public User UpdateEmail(EmailAddress newEmail)
    {
        Email = newEmail;
        return this;
    }

    public User MarkWelcomeEmailSent(DateTimeOffset sentAtUtc)
    {
        WelcomeEmailSentAtUtc ??= sentAtUtc.ToUniversalTime();
        return this;
    }

    public User UpdatePhoneNumber(PhoneNumber newPhoneNumber)
    {
        PhoneNumber = newPhoneNumber;
        return this;
    }
}
