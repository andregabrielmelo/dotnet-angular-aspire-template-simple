using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.ValueObjects;
using AppTemplate.UseCases.Authentication;

namespace AppTemplate.UseCases.Users.Register;

/// <summary>Creates an account and signs it in.</summary>
public record RegisterUserCommand(UserName Name, EmailAddress Email, string Password)
    : ICommand<Result<AuthSession>>;
