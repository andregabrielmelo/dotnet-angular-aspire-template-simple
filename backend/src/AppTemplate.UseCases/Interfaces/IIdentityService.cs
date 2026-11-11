using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.ValueObjects;
using AppTemplate.UseCases.Authentication;
using AppTemplate.UseCases.Authorization;

namespace AppTemplate.UseCases.Interfaces;

/// <summary>
/// Accounts: credentials, email confirmation, password reset, roles and lockout. Implemented in
/// Infrastructure over ASP.NET Core Identity (ADR 013); nothing here exposes an Identity type.
/// An account belongs to one <see cref="User"/> profile and is addressed by its <see cref="UserId"/>.
/// Identity's own failures come back as <c>Error</c> (IdentityResultExtensions).
/// </summary>
public interface IIdentityService
{
    /// <summary>The account of a profile; <c>null</c> when the user was deleted.</summary>
    Task<AccountDto?> GetUserAsync(UserId userId, CancellationToken cancellationToken);

    /// <summary>
    /// Creates the profile and its unconfirmed account, in the <see cref="Role.Member"/> role, in
    /// one transaction: registration. <c>Error</c> when Identity refuses the account (a taken
    /// email, a password it rejects).
    /// </summary>
    Task<Result<UserId>> CreateUserAsync(
        UserName name,
        EmailAddress email,
        string password,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// The same, in any role and optionally already confirmed: seed data and tests use it.
    /// </summary>
    Task<Result<UserId>> CreateUserAsync(
        UserName name,
        EmailAddress email,
        string password,
        Role role,
        bool emailConfirmed,
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

    /// <summary>
    /// Signs the account in for the current request: Identity sets its session cookie, with the
    /// claims its claims factory builds (roles, permissions, the profile's id).
    /// </summary>
    Task SignInAsync(UserId userId, CancellationToken cancellationToken);

    /// <summary>
    /// Reissues the current session's cookie, with the account's new security stamp, so this
    /// session stays signed in after a password change.
    /// </summary>
    Task RefreshSignInAsync(UserId userId, CancellationToken cancellationToken);

    /// <summary>Ends the current session: Identity deletes its cookie.</summary>
    Task SignOutAsync();

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
    /// so Identity ends every session at its next stamp check.
    /// </summary>
    Task<Result> ResetPasswordAsync(
        EmailAddress email,
        string code,
        string newPassword,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// <c>Invalid</c> (<c>currentPassword</c>) for a wrong current password. Changes the
    /// security stamp, so Identity ends every session at its next stamp check.
    /// </summary>
    Task<Result> ChangePasswordAsync(
        UserId userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken
    );
}
