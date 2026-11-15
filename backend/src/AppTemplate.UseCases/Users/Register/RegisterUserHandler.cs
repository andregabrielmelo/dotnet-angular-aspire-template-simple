using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.Aggregates.UserAggregate.Events;
using AppTemplate.UseCases.Authorization;
using AppTemplate.UseCases.Caching;
using AppTemplate.UseCases.Interfaces;
using Microsoft.Extensions.Caching.Hybrid;

namespace AppTemplate.UseCases.Users.Register;

/// <summary>
/// Creates the profile and its account (<see cref="IIdentityService"/>), adds the account to
/// <see cref="Role.Member"/>, emails the confirmation link, then announces the registration
/// (<see cref="UserRegisteredDomainEvent"/>, handled in Core). The email is sent here rather than
/// from a domain event handler because it needs <see cref="IIdentityService"/>, a UseCases port
/// that Core's handlers can't see; if sending fails, the request fails, and the user can ask for
/// another link (<c>POST /auth/resend-confirmation</c>). Joining the role is a second save: if it
/// fails, the account stays without a role (and so without permissions), and nothing is sent or
/// announced. Last, it drops the cached user list (<see cref="CacheTags.UserList"/>), which the new
/// user now belongs to.
/// </summary>
public class RegisterUserHandler(
    IIdentityService _identity,
    IMediator _mediator,
    HybridCache _cache
) : ICommandHandler<RegisterUserCommand, Result<UserId>>
{
    public async ValueTask<Result<UserId>> Handle(
        RegisterUserCommand command,
        CancellationToken cancellationToken
    )
    {
        var created = await _identity.CreateUserAsync(
            command.Name,
            command.Email,
            command.Password,
            cancellationToken
        );
        if (!created.IsSuccess)
        {
            return created;
        }

        var inRole = await _identity.AddToRolesAsync(
            created.Value,
            [Role.Member],
            cancellationToken
        );
        if (!inRole.IsSuccess)
        {
            return inRole;
        }

        // TODO: Maybe this should be done in a domain event handler, but the handler would need to know about the identity, which is an infrastructure concern
        await _identity.SendEmailConfirmationAsync(created.Value, cancellationToken);

        await _mediator.Publish(
            new UserRegisteredDomainEvent(created.Value, command.Name, command.Email),
            cancellationToken
        );

        await _cache.RemoveByTagAsync(CacheTags.UserList, cancellationToken);

        return created;
    }
}
