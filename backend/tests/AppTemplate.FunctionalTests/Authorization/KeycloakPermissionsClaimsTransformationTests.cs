using System.Security.Claims;
using AppTemplate.UseCases.Authorization;
using AppTemplate.Web.Authorization;
using Microsoft.Extensions.Options;
using Xunit;

namespace AppTemplate.FunctionalTests.Authorization;

public class KeycloakPermissionsClaimsTransformationTests
{
    private readonly KeycloakPermissionsClaimsTransformation _transformation = new(
        Options.Create(new KeycloakAuthorizationOptions { ApiClientId = "apptemplate-api" })
    );

    private static ClaimsPrincipal Principal(string? resourceAccess, bool authenticated = true)
    {
        var claims = new List<Claim> { new("sub", "user-1") };
        if (resourceAccess is not null)
        {
            claims.Add(new Claim("resource_access", resourceAccess, "JSON"));
        }
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticated ? "Bearer" : null));
    }

    private static string[] PermissionsOf(ClaimsPrincipal principal) =>
        principal
            .FindAll(KeycloakPermissionsClaimsTransformation.PermissionClaimType)
            .Select(c => c.Value)
            .Order()
            .ToArray();

    [Fact]
    public async Task MapsThisApisClientRolesToPermissions()
    {
        var principal = Principal(
            """{"apptemplate-api":{"roles":["users:read","users:delete"]},"account":{"roles":["manage-account"]}}"""
        );

        var result = await _transformation.TransformAsync(principal);

        Assert.Equal([Permission.UsersDelete, Permission.UsersRead], PermissionsOf(result));
    }

    [Fact]
    public async Task IgnoresRolesOfOtherClientsAndUnknownPermissions()
    {
        var principal = Principal(
            """{"other-api":{"roles":["users:delete"]},"apptemplate-api":{"roles":["superuser"]}}"""
        );

        var result = await _transformation.TransformAsync(principal);

        Assert.Empty(PermissionsOf(result));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not json")]
    [InlineData("""{"apptemplate-api":{"roles":"users:read"}}""")]
    [InlineData("""[1,2,3]""")]
    public async Task MissingOrMalformedClaim_GrantsNothing(string? resourceAccess)
    {
        var result = await _transformation.TransformAsync(Principal(resourceAccess));

        Assert.Empty(PermissionsOf(result));
    }

    [Fact]
    public async Task RunningTwice_DoesNotDuplicatePermissions()
    {
        var principal = Principal("""{"apptemplate-api":{"roles":["users:read"]}}""");

        var once = await _transformation.TransformAsync(principal);
        var twice = await _transformation.TransformAsync(once);

        Assert.Equal([Permission.UsersRead], PermissionsOf(twice));
    }

    [Fact]
    public async Task AnonymousPrincipal_IsLeftUntouched()
    {
        var principal = Principal(
            """{"apptemplate-api":{"roles":["users:read"]}}""",
            authenticated: false
        );

        var result = await _transformation.TransformAsync(principal);

        Assert.Same(principal, result);
    }
}
