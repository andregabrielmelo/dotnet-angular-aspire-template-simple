using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.ValueObjects;

namespace AppTemplate.UseCases.Users.GetOrCreateCurrent;

public record GetOrCreateCurrentUserCommand(string ExternalId, UserName Name, EmailAddress Email)
    : ICommand<Result<CurrentUserDto>>;
