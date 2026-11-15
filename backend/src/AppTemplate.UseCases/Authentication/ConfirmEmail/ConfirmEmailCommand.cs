using AppTemplate.Core.Aggregates.UserAggregate;

namespace AppTemplate.UseCases.Authentication.ConfirmEmail;

/// <param name="Code">The code from the confirmation link.</param>
public record ConfirmEmailCommand(UserId UserId, string Code) : ICommand<Result>;
