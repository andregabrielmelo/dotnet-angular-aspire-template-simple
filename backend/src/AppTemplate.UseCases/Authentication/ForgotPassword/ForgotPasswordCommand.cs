using AppTemplate.Core.ValueObjects;

namespace AppTemplate.UseCases.Authentication.ForgotPassword;

public record ForgotPasswordCommand(EmailAddress Email) : ICommand<Result>;
