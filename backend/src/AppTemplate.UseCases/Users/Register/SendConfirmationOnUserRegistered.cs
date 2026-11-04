using AppTemplate.Core.Aggregates.UserAggregate.Events;
using AppTemplate.UseCases.Authentication;

namespace AppTemplate.UseCases.Users.Register;

/// <summary>
/// Emails a new user the link that confirms their address; they can't sign in before. If sending
/// fails, the account still exists, and the user can ask for another link
/// (<c>POST /auth/resend-confirmation</c>).
/// </summary>
public sealed class SendConfirmationOnUserRegistered(IIdentityService _identity)
    : IDomainEventHandler<UserRegisteredDomainEvent>
{
    public async ValueTask Handle(
        UserRegisteredDomainEvent domainEvent,
        CancellationToken cancellationToken
    ) => await _identity.SendEmailConfirmationAsync(domainEvent.UserId, cancellationToken);
}
