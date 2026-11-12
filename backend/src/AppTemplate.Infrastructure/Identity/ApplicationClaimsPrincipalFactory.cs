using System.Security.Claims;
using AppTemplate.UseCases.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace AppTemplate.Infrastructure.Identity;

/// <summary>
/// The session's claims: Identity's own (account id, email, security stamp and <c>role</c> claims),
/// plus the profile's id and one <c>permissions</c> claim per permission the roles grant, which
/// FastEndpoints' <c>Permissions(...)</c> checks. Identity calls it at sign-in and whenever it
/// revalidates the security stamp, so a role change reaches a session within that interval.
/// </summary>
public sealed class ApplicationClaimsPrincipalFactory(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<int>> roleManager,
    IOptions<IdentityOptions> options
)
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole<int>>(
        userManager,
        roleManager,
        options
    )
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim(AppClaimTypes.UserId, user.ProfileId.Value.ToString()));

        var roles = await UserManager.GetRolesAsync(user);
        foreach (var permission in Role.PermissionsFor(roles))
        {
            identity.AddClaim(new Claim(AppClaimTypes.Permission, permission.Value));
        }

        return identity;
    }
}
