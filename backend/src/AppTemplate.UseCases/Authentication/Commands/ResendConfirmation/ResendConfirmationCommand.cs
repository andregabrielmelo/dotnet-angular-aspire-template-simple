using AppTemplate.Core.ValueObjects;

namespace AppTemplate.UseCases.Authentication.Commands.ResendConfirmation;

public record ResendConfirmationCommand(EmailAddress Email) : ICommand<Result>;
