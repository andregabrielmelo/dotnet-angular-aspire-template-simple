using AppTemplate.Core.Aggregates.UserAggregate.Events;
using AppTemplate.UseCases.Interfaces;

namespace AppTemplate.UseCases.Users.Handlers;

// TODO: Review this and see if it can be moved to the core with the other handlers. Right now, not moved there because core does not now about IIdentityService
/// <summary>
/// Welcomes a new user with the link that confirms their address; they can't sign in before. If
/// sending fails, the account still exists, and the user can ask for another link
/// (<c>POST /auth/resend-confirmation</c>). Public, because Mediator's source generator registers
/// it from the Web project.
/// </summary>
public sealed class SendConfirmationOnUserRegistered(IIdentityService _identity)
    : IDomainEventHandler<UserRegisteredDomainEvent>
{
    public async ValueTask Handle(
        UserRegisteredDomainEvent domainEvent,
        CancellationToken cancellationToken
    ) => await _identity.SendEmailConfirmationAsync(domainEvent.UserId, cancellationToken);
}
