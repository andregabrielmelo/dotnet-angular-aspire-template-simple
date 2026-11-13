using AppTemplate.UseCases.Interfaces;

namespace AppTemplate.UseCases.Authentication.ForgotPassword;

/// <summary>
/// Emails a password reset link to a confirmed account. Always succeeds, whether or not the email
/// has an account, so the answer reveals nothing.
/// </summary>
public class ForgotPasswordHandler(IIdentityService _identity)
    : ICommandHandler<ForgotPasswordCommand, Result>
{
    public async ValueTask<Result> Handle(
        ForgotPasswordCommand command,
        CancellationToken cancellationToken
    )
    {
        await _identity.SendPasswordResetAsync(command.Email, cancellationToken);
        return Result.Success();
    }
}
