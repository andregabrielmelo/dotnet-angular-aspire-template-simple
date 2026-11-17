using AppTemplate.UseCases.Interfaces;

namespace AppTemplate.UseCases.Authentication.ResetPassword;

/// <summary>Sets a new password from a reset link; every existing session ends at its next refresh.</summary>
public class ResetPasswordHandler(IIdentityService identity)
    : ICommandHandler<ResetPasswordCommand, Result>
{
    public async ValueTask<Result> Handle(
        ResetPasswordCommand command,
        CancellationToken cancellationToken
    ) =>
        await identity.ResetPasswordAsync(
            command.Email,
            command.Code,
            command.NewPassword,
            cancellationToken
        );
}
