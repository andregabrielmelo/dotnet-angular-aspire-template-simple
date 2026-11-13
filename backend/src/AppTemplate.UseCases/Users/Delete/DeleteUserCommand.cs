using AppTemplate.Core.Aggregates.UserAggregate;

namespace AppTemplate.UseCases.Users.Delete;

public record DeleteUserCommand(UserId UserId) : Mediator.ICommand<Result>;
