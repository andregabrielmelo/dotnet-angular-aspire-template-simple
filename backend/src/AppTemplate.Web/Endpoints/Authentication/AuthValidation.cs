using AppTemplate.Core.ValueObjects;

namespace AppTemplate.Web.Authentication;

public static class AuthValidation
{
    public const int PasswordMinLength = 8;

    // Hashing cost grows with length; nobody needs more.
    public const int PasswordMaxLength = 128;

    public static IRuleBuilderOptions<T, string> ValidEmail<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .WithMessage("Email is required")
            .MaximumLength(320)
            .Must(EmailAddress.IsValid)
            .WithMessage("Email must be a valid email address");
}
