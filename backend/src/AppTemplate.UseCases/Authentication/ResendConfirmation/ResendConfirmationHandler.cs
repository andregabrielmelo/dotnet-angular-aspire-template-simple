namespace AppTemplate.UseCases.Authentication.ResendConfirmation;

/// <summary>
/// Sends another confirmation link. Always succeeds, whether or not the email has an account,
/// so the answer reveals nothing.
/// </summary>
public class ResendConfirmationHandler(IIdentityService _identity)
    : ICommandHandler<ResendConfirmationCommand, Result>
{
    public async ValueTask<Result> Handle(
        ResendConfirmationCommand command,
        CancellationToken cancellationToken
    )
    {
        await _identity.SendEmailConfirmationAsync(command.Email, cancellationToken);
        return Result.Success();
    }
}
