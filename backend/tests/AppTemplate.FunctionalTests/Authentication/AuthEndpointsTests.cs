using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.SharedKernel;
using AppTemplate.UseCases.Authorization;
using AppTemplate.Web.Authentication;
using AppTemplate.Web.Features.UserFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace AppTemplate.FunctionalTests.Authentication;

/// <summary>
/// Sign-in, registration, refresh and sign-out over the real endpoints, cookies and JWT
/// validation, as the SPA uses them. Tests that move the session clock are in
/// <see cref="AuthSessionLifetimeTests"/>, so the access tokens here stay valid.
/// </summary>
[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class AuthEndpointsTests(AppTemplateWebApplicationFactory factory)
    : IClassFixture<AppTemplateWebApplicationFactory>
{
    private static string NewEmail() => $"auth-{Guid.NewGuid():N}@example.com";

    private async Task<BrowserSession> SignedInAsync(UserRole role = UserRole.Member)
    {
        var user = await factory.CreateUserAsync(role);
        var session = new BrowserSession(factory);
        Assert.Equal(HttpStatusCode.OK, (await session.LoginAsync(user.Email.Value)).StatusCode);
        return session;
    }

    // --- Registration and sign-in -----------------------------------------------------------

    [Fact]
    public async Task Register_SignsIn_SetsAStrictRefreshCookie_AndSendsAWelcomeEmail()
    {
        var email = NewEmail();
        var session = new BrowserSession(factory);

        var response = await session.RegisterAsync(email);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(15 * 60, session.ExpiresIn);
        var setCookie = Assert.Single(
            response.Headers.GetValues("Set-Cookie"),
            header => header.StartsWith($"{AuthCookies.RefreshCookieName}=")
        );
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            $"path={AppTemplateWebApplicationFactory.RefreshCookiePath}",
            setCookie,
            StringComparison.OrdinalIgnoreCase
        );
        Assert.Contains(
            factory.EmailSender.Sent,
            sent => sent.To == email && sent.Subject == "Welcome!"
        );
    }

    [Fact]
    public async Task Register_WithATakenEmail_InAnyCase_IsAConflict()
    {
        var email = NewEmail();
        await new BrowserSession(factory).RegisterAsync(email);

        var response = await new BrowserSession(factory).RegisterAsync(email.ToUpperInvariant());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("not-an-email", "long enough password")]
    [InlineData("ada@example.com", "short")]
    public async Task Register_WithInvalidInput_IsAValidationProblem(string email, string password)
    {
        var response = await new BrowserSession(factory).RegisterAsync(email, password);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithTheWrongPassword_IsUnauthorized()
    {
        var user = await factory.CreateUserAsync();

        var response = await new BrowserSession(factory).LoginAsync(
            user.Email.Value,
            "wrong-password"
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithAnUnknownEmail_LooksTheSameAsAWrongPassword()
    {
        var unknown = await new BrowserSession(factory).LoginAsync(NewEmail(), "wrong-password");
        var user = await factory.CreateUserAsync();
        var wrong = await new BrowserSession(factory).LoginAsync(
            user.Email.Value,
            "wrong-password"
        );

        Assert.Equal(wrong.StatusCode, unknown.StatusCode);
        Assert.Equal(
            Strip(await wrong.Content.ReadAsStringAsync()),
            Strip(await unknown.Content.ReadAsStringAsync())
        );

        // The traceId differs per request; nothing else may.
        static string Strip(string body) =>
            System.Text.RegularExpressions.Regex.Replace(body, "\"traceId\":\"[^\"]*\"", "");
    }

    // --- The access token -------------------------------------------------------------------

    [Fact]
    public async Task AccessToken_AuthenticatesAsTheUser_WithTheirRolesPermissions()
    {
        var session = await SignedInAsync(UserRole.Admin);

        var me = await (
            await session.GetWithAccessTokenAsync("/users/me")
        ).Content.ReadFromJsonAsync<CurrentUserResponse>();
        var users = await session.GetWithAccessTokenAsync("/users");

        Assert.Equal(Permission.All.Order(StringComparer.Ordinal), me!.Permissions);
        Assert.Equal(HttpStatusCode.OK, users.StatusCode);
    }

    [Fact]
    public async Task AMembersAccessToken_DoesNotPassPermissionPolicies()
    {
        var session = await SignedInAsync();

        Assert.Equal(
            HttpStatusCode.OK,
            (await session.GetWithAccessTokenAsync("/users/me")).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await session.GetWithAccessTokenAsync("/users")).StatusCode
        );
    }

    [Fact]
    public async Task AccessToken_SignedWithAnotherKey_IsUnauthorized()
    {
        var forged = Token(RandomNumberGenerator.GetBytes(48), DateTime.UtcNow.AddMinutes(10));

        var response = await new BrowserSession(factory).GetWithAccessTokenAsync(
            "/users/me",
            forged
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AccessToken_ThatExpired_IsUnauthorized()
    {
        var expired = Token(
            Convert.FromBase64String(AppTemplateWebApplicationFactory.SigningKey),
            DateTime.UtcNow.AddMinutes(-5)
        );

        var response = await new BrowserSession(factory).GetWithAccessTokenAsync(
            "/users/me",
            expired
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AccessToken_WithTheRightKey_IsAccepted()
    {
        // The control for the two tests above: only the key or the expiry differs.
        var user = await factory.CreateUserAsync();
        var valid = Token(
            Convert.FromBase64String(AppTemplateWebApplicationFactory.SigningKey),
            DateTime.UtcNow.AddMinutes(10),
            user.Id.Value.ToString()
        );

        var response = await new BrowserSession(factory).GetWithAccessTokenAsync(
            "/users/me",
            valid
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static string Token(byte[] key, DateTime expires, string subject = "1") =>
        new JsonWebTokenHandler().CreateToken(
            new SecurityTokenDescriptor
            {
                Issuer = "apptemplate",
                Audience = "apptemplate-api",
                Subject = new ClaimsIdentity([new Claim("sub", subject)]),
                IssuedAt = expires.AddMinutes(-15),
                NotBefore = expires.AddMinutes(-15),
                Expires = expires,
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256
                ),
            }
        );

    // --- Refresh ----------------------------------------------------------------------------

    [Fact]
    public async Task Refresh_RotatesTheCookie_AndIssuesANewAccessToken()
    {
        var session = await SignedInAsync();
        var firstCookie = session.RefreshToken;
        var firstToken = session.AccessToken;

        var response = await session.RefreshAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEqual(firstCookie, session.RefreshToken);
        Assert.NotEqual(firstToken, session.AccessToken);
        Assert.Equal(
            HttpStatusCode.OK,
            (await session.GetWithAccessTokenAsync("/users/me")).StatusCode
        );
        Assert.Equal(HttpStatusCode.OK, (await session.RefreshAsync()).StatusCode);
    }

    [Fact]
    public async Task TwoSimultaneousRefreshes_OneWins_TheOtherIsAConflict_AndTheSessionSurvives()
    {
        var session = await SignedInAsync();
        await session.EnsureAntiforgeryAsync();

        // Both send the same cookie, like two tabs refreshing at the same moment.
        var responses = await Task.WhenAll(
            session.Client.SendAsync(session.WithAntiforgery("/auth/refresh")),
            session.Client.SendAsync(session.WithAntiforgery("/auth/refresh"))
        );

        Assert.Equal(
            [HttpStatusCode.OK, HttpStatusCode.Conflict],
            responses.Select(response => response.StatusCode).Order()
        );
        // The cookie jar now holds the winner's token, and the family was not revoked.
        Assert.Equal(HttpStatusCode.OK, (await session.RefreshAsync()).StatusCode);
    }

    [Fact]
    public async Task Refresh_PicksUpARoleChange()
    {
        var user = await factory.CreateUserAsync();
        var session = new BrowserSession(factory);
        await session.LoginAsync(user.Email.Value);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await session.GetWithAccessTokenAsync("/users")).StatusCode
        );

        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<IRepository<User>>();
            var stored = await users.GetByIdAsync(user.Id);
            await users.UpdateAsync(stored!.ChangeRole(UserRole.Admin));
        }
        // The current access token still carries the old permissions until it's replaced.
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await session.GetWithAccessTokenAsync("/users")).StatusCode
        );

        await session.RefreshAsync();

        Assert.Equal(
            HttpStatusCode.OK,
            (await session.GetWithAccessTokenAsync("/users")).StatusCode
        );
    }

    [Fact]
    public async Task Refresh_ForADeletedUser_IsUnauthorized_AndClearsTheCookie()
    {
        var user = await factory.CreateUserAsync();
        var session = new BrowserSession(factory);
        await session.LoginAsync(user.Email.Value);

        await factory
            .CreateAuthenticatedClient(UserId.From(int.MaxValue), Permission.UsersDelete)
            .DeleteAsync($"/users/{user.Id.Value}");
        var response = await session.RefreshAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(session.RefreshToken);
    }

    [Fact]
    public async Task Refresh_WithoutACookie_IsUnauthorized()
    {
        var response = await new BrowserSession(factory).RefreshAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // --- Sign-out ---------------------------------------------------------------------------

    [Fact]
    public async Task Logout_EndsTheSession()
    {
        var session = await SignedInAsync();
        var refreshToken = session.RefreshToken;

        var response = await session.LogoutAsync();

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null(session.RefreshToken);
        session.RefreshToken = refreshToken; // a copy of the cookie is no good either
        Assert.Equal(HttpStatusCode.Unauthorized, (await session.RefreshAsync()).StatusCode);
    }

    [Fact]
    public async Task Logout_LeavesTheUsersOtherSessionsAlone()
    {
        var user = await factory.CreateUserAsync();
        var laptop = new BrowserSession(factory);
        var phone = new BrowserSession(factory);
        await laptop.LoginAsync(user.Email.Value);
        await phone.LoginAsync(user.Email.Value);

        await laptop.LogoutAsync();

        Assert.Equal(HttpStatusCode.OK, (await phone.RefreshAsync()).StatusCode);
    }

    // --- Cross-site protection of the cookie-authenticated endpoints -------------------------

    public static TheoryData<string> CookieEndpoints() => ["/auth/refresh", "/auth/logout"];

    [Theory]
    [MemberData(nameof(CookieEndpoints))]
    public async Task FromAForeignOrigin_IsForbidden(string path)
    {
        var session = await SignedInAsync();
        var request = session.WithAntiforgery(path);
        request.Headers.Remove("Origin");
        request.Headers.Add("Origin", "https://evil.app.test");
        session.Client.DefaultRequestHeaders.Remove("Origin");

        var response = await session.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(CookieEndpoints))]
    public async Task WithoutAnOrigin_IsForbidden(string path)
    {
        var session = await SignedInAsync();
        session.Client.DefaultRequestHeaders.Remove("Origin");

        var response = await session.Client.SendAsync(session.WithAntiforgery(path));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(CookieEndpoints))]
    public async Task WithoutTheAntiforgeryHeader_IsForbidden(string path)
    {
        var session = await SignedInAsync();

        var response = await session.Client.PostAsync(path, content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(CookieEndpoints))]
    public async Task WithAWrongAntiforgeryHeader_IsForbidden(string path)
    {
        var session = await SignedInAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add(AuthCookies.AntiforgeryHeaderName, "forged-token");

        var response = await session.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
