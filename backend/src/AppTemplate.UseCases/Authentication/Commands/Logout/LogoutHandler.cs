using AppTemplate.UseCases.Interfaces;

namespace AppTemplate.UseCases.Authentication.Commands.Logout;

/// <summary>
/// Signs out, which deletes the session cookie. Like any cookie sign-out it doesn't revoke a copy
/// of the cookie; changing the password does (ADR 015).
/// </summary>
public class LogoutHandler(IIdentityService _identity) : ICommandHandler<LogoutCommand, Result>
{
    public async ValueTask<Result> Handle(
        LogoutCommand command,
        CancellationToken cancellationToken
    )
    {
        await _identity.SignOutAsync();
        return Result.Success();
    }
}
