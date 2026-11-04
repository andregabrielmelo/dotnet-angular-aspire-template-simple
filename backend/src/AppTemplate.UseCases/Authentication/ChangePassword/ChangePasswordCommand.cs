using AppTemplate.Core.Aggregates.UserAggregate;

namespace AppTemplate.UseCases.Authentication.ChangePassword;

/// <summary>Changes the caller's password; returns them, for the new session the endpoint issues.</summary>
public record ChangePasswordCommand(UserId UserId, string CurrentPassword, string NewPassword)
    : ICommand<Result<AuthenticatedUserDto>>;
