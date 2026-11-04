using AppTemplate.Core.ValueObjects;

namespace AppTemplate.UseCases.Authentication.ResendConfirmation;

public record ResendConfirmationCommand(EmailAddress Email) : ICommand<Result>;
