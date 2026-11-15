using AppTemplate.Core.Aggregates.UserAggregate;

namespace AppTemplate.UseCases.Users.Update;

/// <summary>
/// Updates a user's name and, like a PATCH, the parts of their phone number that are sent: a
/// part left out (null) keeps its saved value. Nothing removes the phone number.
/// </summary>
/// <param name="PhoneNumber">The local number; null or blank keeps the saved one.</param>
/// <param name="PhoneCountryCode">E.g. "+55"; null or blank keeps the saved one. A user without a
/// phone number needs both this and <paramref name="PhoneNumber"/>.</param>
/// <param name="PhoneExtension">Null keeps the saved extension, and empty removes it.</param>
public record UpdateUserCommand(
    UserId UserId,
    UserName UserName,
    string? PhoneNumber,
    string? PhoneCountryCode,
    string? PhoneExtension
) : ICommand<Result<UserDto>>;
