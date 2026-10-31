using System.Security.Claims;
using System.Text.Encodings.Web;
using AppTemplate.Web.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AppTemplate.FunctionalTests;

/// <summary>
/// Stands in for an access token in functional tests: a request carrying
/// <see cref="UserHeader"/> is authenticated as that user id (<c>sub</c>), with the permission
/// claims a real token would carry. Requests without it are forwarded to the real JWT bearer
/// handler (see <see cref="AppTemplateWebApplicationFactory"/>).
/// </summary>
public class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder
) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string UserHeader = "X-Test-User";

    /// <summary>Comma-separated API permissions, emitted as <c>permission</c> claims.</summary>
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
            new("email", $"user-{subject}@example.com"),
        };
        claims.AddRange(
            Request
                .Headers[PermissionsHeader]
                .ToString()
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(permission => new Claim(AppClaimTypes.Permission, permission))
        );

        var identity = new ClaimsIdentity(claims, SchemeName, "name", "role");

        return Task.FromResult(
            AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)
            )
        );
    }
}
