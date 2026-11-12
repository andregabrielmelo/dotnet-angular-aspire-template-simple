using AppTemplate.Core.Aggregates.UserAggregate;

namespace AppTemplate.UseCases.Users.Commands.Delete;

public record DeleteUserCommand(UserId UserId) : Mediator.ICommand<Result>;
