using AppTemplate.UseCases.Interfaces;

namespace AppTemplate.UseCases.Authentication.Commands.ConfirmEmail;

/// <summary>Confirms the address the link was sent to; the account can then sign in.</summary>
public class ConfirmEmailHandler(IIdentityService _identity)
    : ICommandHandler<ConfirmEmailCommand, Result>
{
    public async ValueTask<Result> Handle(
        ConfirmEmailCommand command,
        CancellationToken cancellationToken
    ) => await _identity.ConfirmEmailAsync(command.UserId, command.Code, cancellationToken);
}
