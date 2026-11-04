using AppTemplate.Core.ValueObjects;

namespace AppTemplate.UseCases.Authentication.Login;

public record LoginCommand(EmailAddress Email, string Password)
    : ICommand<Result<AuthenticatedUserDto>>;
