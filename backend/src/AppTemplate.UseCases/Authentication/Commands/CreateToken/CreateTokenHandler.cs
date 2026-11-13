using AppTemplate.UseCases.Interfaces;

namespace AppTemplate.UseCases.Authentication.Commands.CreateToken;

/// <summary>
/// Checks the credentials exactly as signing in does (<c>LoginHandler</c>: lockout included, one
/// answer for an unknown email, a wrong password and a locked-out account, <c>Forbidden</c> for an
/// unconfirmed email), then issues a token instead of setting the cookie.
/// </summary>
public class CreateTokenHandler(IIdentityService _identity, ITokenClaimsService _tokens)
    : ICommandHandler<CreateTokenCommand, Result<AccessToken>>
{
    public async ValueTask<Result<AccessToken>> Handle(
        CreateTokenCommand command,
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

        return await _tokens.GetTokenAsync(account.Value.Id, cancellationToken);
    }
}
