using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.UseCases.Caching;
using Microsoft.Extensions.Caching.Hybrid;

namespace AppTemplate.UseCases.Users.Commands.Delete;

/// <summary>Deletes the user (and, by cascade, their account), then drops their cached reads (<see cref="CacheTags.User"/>) and the cached user list.</summary>
public class DeleteUserHandler(IRepository<User> _repository, HybridCache _cache)
    : ICommandHandler<DeleteUserCommand, Result>
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
        await _cache.RemoveByTagAsync(
            [CacheTags.User(user.Id), CacheTags.UserList],
            cancellationToken
        );
        return Result.Success();
    }
}
