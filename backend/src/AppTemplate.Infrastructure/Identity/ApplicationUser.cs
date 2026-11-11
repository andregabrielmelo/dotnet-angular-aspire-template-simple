using AppTemplate.Core.Aggregates.UserAggregate;
using Microsoft.AspNetCore.Identity;

namespace AppTemplate.Infrastructure.Identity;

/// <summary>
/// A person's account: ASP.NET Core Identity's user (email, password hash, lockout, security
/// stamp, roles). It belongs to one domain <see cref="User"/> profile, <see cref="ProfileId"/>,
/// whose id the rest of the application uses: the session's <c>user_id</c> claim is the profile id,
/// never this account's own <c>Id</c>. Deleting the profile deletes the account (cascade).
/// </summary>
public class ApplicationUser : IdentityUser<int>
{
    public UserId ProfileId { get; set; }
}
