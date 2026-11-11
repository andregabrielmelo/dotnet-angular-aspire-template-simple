using AppTemplate.UseCases.Interfaces;

namespace AppTemplate.UseCases.Authentication.Commands.Login;

/// <summary>
/// Checks the credentials with Identity (lockout included), then signs the account in, which sets
/// Identity's session cookie. An unknown email, a wrong password and a locked-out account get the
/// same answer, so the response doesn't reveal who has an account; only someone who knows the
/// password learns that it still needs confirming.
/// </summary>
public class LoginHandler(IIdentityService _identity) : ICommandHandler<LoginCommand, Result>
{
    public async ValueTask<Result> Handle(LoginCommand command, CancellationToken cancellationToken)
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

        await _identity.SignInAsync(account.Value.Id, cancellationToken);
        return Result.Success();
    }
}
