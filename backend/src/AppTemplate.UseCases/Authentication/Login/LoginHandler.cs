using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.Aggregates.UserAggregate.Specifications;

namespace AppTemplate.UseCases.Authentication.Login;

/// <summary>
/// Checks the credentials. An unknown email and a wrong password get the same answer, so the
/// response doesn't reveal who has an account.
/// </summary>
public class LoginHandler(IRepository<User> _users, IPasswordHasher _passwordHasher)
    : ICommandHandler<LoginCommand, Result<AuthenticatedUserDto>>
{
    public async ValueTask<Result<AuthenticatedUserDto>> Handle(
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

        return AuthenticatedUserDto.FromEntity(user);
    }
}
