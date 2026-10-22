using AppTemplate.Core.Aggregates.UserAggregate;

namespace AppTemplate.UseCases.Users.Delete;

public class DeleteUserHandler(IRepository<User> _repository)
    : Mediator.ICommandHandler<DeleteUserCommand, Result>
{
    public async ValueTask<Result> Handle(
        DeleteUserCommand request,
        CancellationToken cancellationToken
    )
    {
        var user = await _repository.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
            return Result.NotFound();

        await _repository.DeleteAsync(user, cancellationToken);
        return Result.Success();
    }
}
