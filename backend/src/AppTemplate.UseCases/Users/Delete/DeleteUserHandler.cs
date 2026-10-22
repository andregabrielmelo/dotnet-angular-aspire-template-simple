using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.UseCases.Caching;

namespace AppTemplate.UseCases.Users.Delete;

public class DeleteUserHandler(IRepository<User> _repository, ICacheInvalidator _cacheInvalidator)
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
        await _cacheInvalidator.InvalidateAsync(CacheTags.Users, cancellationToken);
        return Result.Success();
    }
}
