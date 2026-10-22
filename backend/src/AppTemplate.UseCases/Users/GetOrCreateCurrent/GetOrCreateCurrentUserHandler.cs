using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.Aggregates.UserAggregate.Specifications;
using AppTemplate.Core.ValueObjects;
using AppTemplate.UseCases.Caching;
using AppTemplate.UseCases.Telemetry;
using Microsoft.Extensions.Caching.Hybrid;

namespace AppTemplate.UseCases.Users.GetOrCreateCurrent;

public record CurrentUserDto(UserId Id, UserName Name, EmailAddress Email);

/// <summary>
/// Just-in-time provisioning: registration happens in the OpenID Connect provider (Keycloak),
/// so the domain <see cref="User"/> row is created the first time that identity calls the API.
/// Called on every page load of the SPA, so the existing-user lookup goes through HybridCache.
/// </summary>
public class GetOrCreateCurrentUserHandler(
    IRepository<User> _repository,
    HybridCache _cache,
    ICacheInvalidator _cacheInvalidator,
    ApplicationMetrics _metrics
) : ICommandHandler<GetOrCreateCurrentUserCommand, Result<CurrentUserDto>>
{
    public async ValueTask<Result<CurrentUserDto>> Handle(
        GetOrCreateCurrentUserCommand command,
        CancellationToken cancellationToken
    )
    {
        var existingUser = await _cache.GetOrCreateExistingAsync(
            CachedUser.KeyByExternalId(command.ExternalId),
            (_repository, command.ExternalId),
            static async (state, token) =>
            {
                var (repository, externalId) = state;
                var entity = await repository.FirstOrDefaultAsync(
                    new UserByExternalIdSpecification(externalId),
                    token
                );
                return entity is null ? null : CachedUser.FromEntity(entity);
            },
            CachedUser.EntryOptions,
            CachedUser.Tags,
            cancellationToken
        );
        if (existingUser is not null)
        {
            return new CurrentUserDto(
                UserId.From(existingUser.Id),
                UserName.From(existingUser.Name),
                new EmailAddress(existingUser.Email)
            );
        }

        // Email is unique in the domain model too - a different identity already claiming this
        // address (e.g. a seed fixture, or an account re-created in Keycloak) is a conflict.
        var emailTaken = await _repository.AnyAsync(
            new UserByEmailSpecification(command.Email),
            cancellationToken
        );
        if (emailTaken)
        {
            return Result<CurrentUserDto>.Conflict("Email is already linked to another user");
        }

        var newUser = User.Create(command.ExternalId, command.Name, command.Email);
        var createdUser = await _repository.AddAsync(newUser, cancellationToken);
        _metrics.UserProvisioned();

        // User lists cached before this user existed are now stale.
        await _cacheInvalidator.InvalidateAsync(CacheTags.Users, cancellationToken);

        return ToDto(createdUser);
    }

    private static CurrentUserDto ToDto(User user) => new(user.Id, user.Name, user.Email);
}
