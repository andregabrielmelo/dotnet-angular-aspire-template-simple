using AppTemplate.Core.Aggregates.UserAggregate;

namespace AppTemplate.UseCases.Users.Queries.Get;

public record GetUserQuery(UserId UserId) : IQuery<Result<UserDto>>;
