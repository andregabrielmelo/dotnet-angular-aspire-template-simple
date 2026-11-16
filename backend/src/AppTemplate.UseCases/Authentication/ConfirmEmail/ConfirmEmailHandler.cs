using AppTemplate.UseCases.Interfaces;

namespace AppTemplate.UseCases.Authentication.ConfirmEmail;

/// <summary>Confirms the address the link was sent to; the account can then sign in.</summary>
public class ConfirmEmailHandler(IIdentityService identity)
    : ICommandHandler<ConfirmEmailCommand, Result>
{
    public async ValueTask<Result> Handle(
        ConfirmEmailCommand command,
        CancellationToken cancellationToken
    ) => await identity.ConfirmEmailAsync(command.UserId, command.Code, cancellationToken);
}
