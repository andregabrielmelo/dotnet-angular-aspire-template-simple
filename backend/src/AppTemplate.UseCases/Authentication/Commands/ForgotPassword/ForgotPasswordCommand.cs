using AppTemplate.Core.ValueObjects;

namespace AppTemplate.UseCases.Authentication.Commands.ForgotPassword;

public record ForgotPasswordCommand(EmailAddress Email) : ICommand<Result>;
