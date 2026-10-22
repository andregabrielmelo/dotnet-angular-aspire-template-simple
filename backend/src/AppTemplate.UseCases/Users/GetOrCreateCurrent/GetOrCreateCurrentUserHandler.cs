using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.Aggregates.UserAggregate.Specifications;
using AppTemplate.Core.ValueObjects;
using AppTemplate.UseCases.Telemetry;

namespace AppTemplate.UseCases.Users.GetOrCreateCurrent;

public record CurrentUserDto(UserId Id, UserName Name, EmailAddress Email);

/// <summary>
/// Just-in-time provisioning: registration happens in the OpenID Connect provider (Keycloak),
/// so the domain <see cref="User"/> row is created the first time that identity calls the API.
/// </summary>
public class GetOrCreateCurrentUserHandler(
    IRepository<User> _repository,
    ApplicationMetrics _metrics
) : ICommandHandler<GetOrCreateCurrentUserCommand, Result<CurrentUserDto>>
{
    public async ValueTask<Result<CurrentUserDto>> Handle(
        GetOrCreateCurrentUserCommand command,
        CancellationToken cancellationToken
    )
    {
        var existingUser = await _repository.FirstOrDefaultAsync(
            new UserByExternalIdSpecification(command.ExternalId),
            cancellationToken
        );
        if (existingUser is not null)
        {
            return ToDto(existingUser);
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

        return ToDto(createdUser);
    }

    private static CurrentUserDto ToDto(User user) => new(user.Id, user.Name, user.Email);
}
