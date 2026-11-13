using AppTemplate.Core.ValueObjects;

namespace AppTemplate.UseCases.Authentication.Commands.CreateToken;

/// <summary>
/// Exchanges a password for a bearer token, for clients that can't hold the session cookie
/// (ADR 018).
/// </summary>
public record CreateTokenCommand(EmailAddress Email, string Password)
    : ICommand<Result<AccessToken>>;
