using AppTemplate.Core.Aggregates.UserAggregate;

namespace AppTemplate.UseCases.Users.Update;

/// <param name="PhoneNumber">Leave empty to keep the current phone number.</param>
/// <param name="PhoneCountryCode">Required with <paramref name="PhoneNumber"/>, e.g. "+55".</param>
public record UpdateUserCommand(
    UserId UserId,
    UserName UserName,
    [property: PersonalData] string? PhoneNumber,
    [property: PersonalData] string? PhoneCountryCode,
    [property: PersonalData] string? PhoneExtension
) : Mediator.ICommand<Result<UserDto>>;
