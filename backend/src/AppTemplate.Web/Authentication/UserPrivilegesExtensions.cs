using AppTemplate.UseCases.Authentication;
using AppTemplate.UseCases.Authorization;
using Microsoft.IdentityModel.JsonWebTokens;

namespace AppTemplate.Web.Authentication;

public static class UserPrivilegesExtensions
{
    /// <summary>
    /// What every access token says about its user, at sign-in, registration and refresh alike:
    /// who they are, a unique <c>jti</c> (FastEndpoints adds none; logout revokes by it), and the
    /// permissions their role grants, which <c>Permissions(...)</c> on an endpoint checks.
    /// </summary>
    public static void AddUser(this UserPrivileges privileges, AuthenticatedUserDto user)
    {
        privileges.Claims.Add(new(JwtRegisteredClaimNames.Sub, user.Id.Value.ToString()));
        privileges.Claims.Add(new(JwtRegisteredClaimNames.Email, user.Email.Value));
        privileges.Claims.Add(new(JwtRegisteredClaimNames.Name, user.Name.Value));
        privileges.Claims.Add(new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")));
        privileges.Permissions.AddRange(RolePermissions.For(user.Role));
    }
}
