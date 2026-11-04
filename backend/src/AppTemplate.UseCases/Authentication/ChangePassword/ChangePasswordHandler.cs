using AppTemplate.Core.Aggregates.UserAggregate;

namespace AppTemplate.UseCases.Authentication.ChangePassword;

/// <summary>
/// Changes the password. The account's security stamp changes with it, so every other session
/// ends at its next refresh; the endpoint gives this one new tokens.
/// </summary>
public class ChangePasswordHandler(IIdentityService _identity, IRepository<User> _users)
    : ICommandHandler<ChangePasswordCommand, Result<AuthenticatedUserDto>>
{
    public async ValueTask<Result<AuthenticatedUserDto>> Handle(
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

        var account = await _identity.GetAccountAsync(command.UserId, cancellationToken);
        var profile = await _users.GetByIdAsync(command.UserId, cancellationToken);
        return account is null || profile is null
            ? Result.NotFound()
            : AuthenticatedUserDto.From(profile, account);
    }
}
