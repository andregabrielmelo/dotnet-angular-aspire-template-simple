using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.ValueObjects;

namespace AppTemplate.UseCases.Users;

public record UserDto(UserId Id, UserName Name, PhoneNumber? PhoneNumber);
