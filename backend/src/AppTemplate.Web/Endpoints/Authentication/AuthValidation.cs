using AppTemplate.Core.ValueObjects;
using AppTemplate.UseCases.Authentication;

namespace AppTemplate.Web.Authentication;

public static class AuthValidation
{
    public static IRuleBuilderOptions<T, string> ValidEmail<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .WithMessage("Email is required")
            .MaximumLength(320)
            .Must(EmailAddress.IsValid)
            .WithMessage("Email must be a valid email address");

    /// <summary>A password being checked: any length the rules ever allowed.</summary>
    public static IRuleBuilderOptions<T, string> ProvidedPassword<T>(
        this IRuleBuilder<T, string> rule
    ) => rule.NotEmpty().WithMessage("Password is required").MaximumLength(PasswordRules.MaxLength);

    /// <summary>A password being set. Identity enforces the same rules; this answers sooner.</summary>
    public static IRuleBuilderOptions<T, string> NewPassword<T>(
        this IRuleBuilder<T, string> rule
    ) =>
        rule.NotEmpty()
            .WithMessage("Password is required")
            .MinimumLength(PasswordRules.MinLength)
            .WithMessage($"Password must be at least {PasswordRules.MinLength} characters")
            .MaximumLength(PasswordRules.MaxLength);

    public static IRuleBuilderOptions<T, string> ValidCode<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("The link is incomplete. Open it from the email again.");
}
