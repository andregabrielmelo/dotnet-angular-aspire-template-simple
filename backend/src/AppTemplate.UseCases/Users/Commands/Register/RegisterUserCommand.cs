using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.ValueObjects;

namespace AppTemplate.UseCases.Users.Commands.Register;

/// <summary>Creates an account; it can sign in once its email is confirmed.</summary>
public record RegisterUserCommand(UserName Name, EmailAddress Email, string Password)
    : ICommand<Result<UserId>>;
