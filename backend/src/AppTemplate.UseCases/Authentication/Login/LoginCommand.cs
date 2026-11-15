using AppTemplate.Core.ValueObjects;

namespace AppTemplate.UseCases.Authentication.Login;

/// <summary>Signs in with a password; on success, Identity sets the session cookie.</summary>
public record LoginCommand(EmailAddress Email, string Password) : ICommand<Result>;
