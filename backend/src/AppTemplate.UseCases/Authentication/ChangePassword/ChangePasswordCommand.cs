using AppTemplate.Core.Aggregates.UserAggregate;

namespace AppTemplate.UseCases.Authentication.ChangePassword;

/// <summary>Changes the caller's password, keeping this session signed in.</summary>
public record ChangePasswordCommand(UserId UserId, string CurrentPassword, string NewPassword)
    : ICommand<Result>;
