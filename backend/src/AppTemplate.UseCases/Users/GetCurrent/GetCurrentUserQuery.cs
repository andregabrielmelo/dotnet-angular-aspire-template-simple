using AppTemplate.Core.Aggregates.UserAggregate;

namespace AppTemplate.UseCases.Users.GetCurrent;

public record GetCurrentUserQuery(UserId UserId) : IQuery<Result<CurrentUserDto>>;
