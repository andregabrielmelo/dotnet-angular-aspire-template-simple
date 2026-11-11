using AppTemplate.Core.Aggregates.UserAggregate;

namespace AppTemplate.UseCases.Users.Queries.GetCurrent;

public record GetCurrentUserQuery(UserId UserId) : IQuery<Result<CurrentUserDto>>;
