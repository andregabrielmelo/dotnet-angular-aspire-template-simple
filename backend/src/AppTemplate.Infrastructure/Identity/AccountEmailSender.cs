using AppTemplate.Core.Interfaces;
using AppTemplate.Infrastructure.Email;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace AppTemplate.Infrastructure.Identity;

/// <summary>
/// Identity's account emails (its <see cref="IEmailSender{TUser}"/>), sent through the
/// application's <see cref="IEmailSender"/> (MailKit). The links come from
/// <see cref="IdentityService"/>; they carry single-use codes, so never log them.
/// </summary>
public sealed class AccountEmailSender(IEmailSender _emailSender, IOptions<MailserverConfiguration> mailserverOptions) : IEmailSender<ApplicationUser>
{
    private readonly MailserverConfiguration _mailserver = mailserverOptions.Value;

    public Task SendConfirmationLinkAsync(
        ApplicationUser user,
        string email,
        string confirmationLink
    ) =>
        _emailSender.SendAsync(
            email,
            _mailserver.FromAddress,
            "Confirm your email",
            "Welcome! Confirm your email address to start using your account:\n\n"
                + $"{confirmationLink}\n\nIf you didn't sign up, you can ignore this email."
        );

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
        _emailSender.SendAsync(
            email,
            _mailserver.FromAddress,
            "Reset your password",
            $"Choose a new password here:\n\n{resetLink}\n\n"
                + "If you didn't ask for this, you can ignore this email; your password is unchanged."
        );

    // Identity's code-only reset flow (MapIdentityApi). This API sends links instead.
    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
        _emailSender.SendAsync(
            email,
            _mailserver.FromAddress,
            "Reset your password",
            $"Your password reset code is: {resetCode}"
        );
}
