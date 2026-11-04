using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.ValueObjects;

namespace AppTemplate.UseCases.Authentication;

/// <summary>
/// Accounts: credentials, email confirmation, password reset, roles and lockout. Implemented in
/// Infrastructure over ASP.NET Core Identity (ADR 013); nothing here exposes an Identity type.
/// An account belongs to one <see cref="User"/> profile and is addressed by its <see cref="UserId"/>.
/// Field errors come back as <c>Invalid</c>, keyed by the request field they belong to.
/// </summary>
public interface IIdentityService
{
    /// <summary>
    /// Creates the profile and its account, in the <see cref="Authorization.Roles.Member"/>
    /// role, in one transaction. <c>Conflict</c> for an email already in use; <c>Invalid</c>
    /// (<c>password</c>) when the password breaks the rules.
    /// </summary>
    Task<Result<UserId>> CreateAccountAsync(
        UserName name,
        EmailAddress email,
        string password,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Checks a sign-in attempt, counting failures toward lockout. A wrong password, an unknown
    /// email and a locked-out account all get the same <c>Unauthorized</c>; a correct password
    /// on an unconfirmed email gets <c>Forbidden</c>.
    /// </summary>
    Task<Result<AccountDto>> CheckPasswordAsync(
        EmailAddress email,
        string password,
        CancellationToken cancellationToken
    );

    Task<AccountDto?> GetAccountAsync(UserId userId, CancellationToken cancellationToken);

    /// <summary>Emails a confirmation link, unless the address is already confirmed.</summary>
    Task SendEmailConfirmationAsync(UserId userId, CancellationToken cancellationToken);

    /// <summary>
    /// The same, by email, for someone who lost the first one. Does nothing for an unknown
    /// email, so the answer never reveals who has an account.
    /// </summary>
    Task SendEmailConfirmationAsync(EmailAddress email, CancellationToken cancellationToken);

    /// <summary><c>Invalid</c> (<c>code</c>) for an unknown account or a wrong or expired code.</summary>
    Task<Result> ConfirmEmailAsync(UserId userId, string code, CancellationToken cancellationToken);

    /// <summary>Emails a password reset link to a confirmed account; otherwise does nothing.</summary>
    Task SendPasswordResetAsync(EmailAddress email, CancellationToken cancellationToken);

    /// <summary>
    /// Sets a new password with a reset code, and lifts any lockout. Changes the security stamp,
    /// so every session ends at its next refresh.
    /// </summary>
    Task<Result> ResetPasswordAsync(
        EmailAddress email,
        string code,
        string newPassword,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// <c>Invalid</c> (<c>currentPassword</c>) for a wrong current password. Changes the
    /// security stamp, so every session ends at its next refresh.
    /// </summary>
    Task<Result> ChangePasswordAsync(
        UserId userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken
    );
}
