using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.UseCases.Authentication;

namespace AppTemplate.UseCases.Interfaces;

/// <summary>
/// Issues bearer tokens for clients that can't hold the session cookie, such as scripts and
/// mobile apps (ADR 018), after NimblePros/eShopOnWeb's <c>ITokenClaimsService</c>. A token
/// carries the same claims as the session: the profile's id, the roles and their permissions.
/// Check the credentials first (<see cref="IIdentityService.CheckPasswordAsync"/>); this only
/// signs.
/// </summary>
public interface ITokenClaimsService
{
    Task<AccessToken> GetTokenAsync(UserId userId, CancellationToken cancellationToken);
}
