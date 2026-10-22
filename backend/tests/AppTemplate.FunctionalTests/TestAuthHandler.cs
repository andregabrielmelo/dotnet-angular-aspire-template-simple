using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AppTemplate.FunctionalTests;

/// <summary>
/// Stands in for Keycloak-issued JWTs in functional tests: a request carrying
/// <see cref="UserHeader"/> is authenticated as that <c>sub</c>, with the same claim names a
/// real access token has. Requests without it stay anonymous.
/// </summary>
public class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder
) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string UserHeader = "X-Test-User";

    /// <summary>Comma-separated API permissions, emitted like Keycloak's resource_access claim.</summary>
    public const string PermissionsHeader = "X-Test-Permissions";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (
            !Request.Headers.TryGetValue(UserHeader, out var subject)
            || string.IsNullOrEmpty(subject)
        )
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>
        {
            new("sub", subject!),
            new("name", $"Test {subject}"),
            new("email", $"{subject}@example.com"),
        };

        var permissions = Request
            .Headers[PermissionsHeader]
            .ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (permissions.Length > 0)
        {
            var resourceAccess = JsonSerializer.Serialize(
                new Dictionary<string, object> { ["apptemplate-api"] = new { roles = permissions } }
            );
            claims.Add(new Claim("resource_access", resourceAccess, "JSON"));
        }

        var identity = new ClaimsIdentity(claims, SchemeName, "name", "roles");

        return Task.FromResult(
            AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)
            )
        );
    }
}
