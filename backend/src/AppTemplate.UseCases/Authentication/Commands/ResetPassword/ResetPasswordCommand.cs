using AppTemplate.Core.ValueObjects;

namespace AppTemplate.UseCases.Authentication.Commands.ResetPassword;

/// <param name="Code">The code from the reset link.</param>
public record ResetPasswordCommand(EmailAddress Email, string Code, string NewPassword)
    : ICommand<Result>;
