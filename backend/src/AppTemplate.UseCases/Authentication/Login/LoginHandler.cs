using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.Aggregates.UserAggregate.Specifications;

namespace AppTemplate.UseCases.Authentication.Login;

/// <summary>
/// An unknown email and a wrong password get the same answer, so the response doesn't reveal
/// who has an account. Repeated attempts are limited by the endpoint's rate limit policy.
/// </summary>
public class LoginHandler(
    IRepository<User> _users,
    IPasswordHasher _passwordHasher,
    SessionFactory _sessions
) : ICommandHandler<LoginCommand, Result<AuthSession>>
{
    public async ValueTask<Result<AuthSession>> Handle(
        LoginCommand command,
        CancellationToken cancellationToken
    )
    {
        var user = await _users.FirstOrDefaultAsync(
            new UserByEmailSpecification(command.Email),
            cancellationToken
        );

        if (user is null)
        {
            // Hash anyway, so an unknown email takes as long as a wrong password.
            _passwordHasher.Hash(command.Password);
            return Result.Unauthorized();
        }

        if (!_passwordHasher.Verify(user.PasswordHash, command.Password))
        {
            return Result.Unauthorized();
        }

        return await _sessions.StartAsync(user, cancellationToken);
    }
}
