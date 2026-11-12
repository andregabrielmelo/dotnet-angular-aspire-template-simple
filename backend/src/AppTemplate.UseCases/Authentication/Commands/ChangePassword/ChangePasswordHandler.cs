using AppTemplate.UseCases.Interfaces;

namespace AppTemplate.UseCases.Authentication.Commands.ChangePassword;

/// <summary>
/// Changes the password. The account's security stamp changes with it, so Identity ends every
/// other session at its next stamp check; this one gets a new cookie.
/// </summary>
public class ChangePasswordHandler(IIdentityService _identity)
    : ICommandHandler<ChangePasswordCommand, Result>
{
    public async ValueTask<Result> Handle(
        ChangePasswordCommand command,
        CancellationToken cancellationToken
    )
    {
        var changed = await _identity.ChangePasswordAsync(
            command.UserId,
            command.CurrentPassword,
            command.NewPassword,
            cancellationToken
        );
        if (!changed.IsSuccess)
        {
            return changed;
        }

        await _identity.RefreshSignInAsync(command.UserId, cancellationToken);
        return Result.Success();
    }
}
