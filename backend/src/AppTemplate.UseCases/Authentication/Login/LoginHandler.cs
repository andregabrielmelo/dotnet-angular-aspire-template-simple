using AppTemplate.Core.Aggregates.UserAggregate;

namespace AppTemplate.UseCases.Authentication.Login;

/// <summary>
/// Checks the credentials with Identity (lockout included). An unknown email, a wrong password
/// and a locked-out account get the same answer, so the response doesn't reveal who has an
/// account; only someone who knows the password learns that it still needs confirming.
/// </summary>
public class LoginHandler(IIdentityService _identity, IRepository<User> _users)
    : ICommandHandler<LoginCommand, Result<AuthenticatedUserDto>>
{
    public async ValueTask<Result<AuthenticatedUserDto>> Handle(
        LoginCommand command,
        CancellationToken cancellationToken
    )
    {
        var account = await _identity.CheckPasswordAsync(
            command.Email,
            command.Password,
            cancellationToken
        );
        if (account.Status == ResultStatus.Forbidden)
        {
            return Result.Forbidden([.. account.Errors]);
        }
        if (!account.IsSuccess)
        {
            return Result.Unauthorized();
        }

        var profile = await _users.GetByIdAsync(account.Value.Id, cancellationToken);
        return profile is null
            ? Result.Unauthorized()
            : AuthenticatedUserDto.From(profile, account.Value);
    }
}
