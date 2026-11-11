using System.Text;
using AppTemplate.Web.Configurations;

namespace AppTemplate.FunctionalTests.Security;

/// <summary>
/// A signed-in session's unsafe requests need the antiforgery token (ADR 015): another page can
/// make the browser send the session cookie, but can't read the token to send it as a header.
/// </summary>
[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class CsrfProtection(CustomWebApplicationFactory<Program> factory)
    : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static HttpRequestMessage Rename(TestUser user, HttpContent content, string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/users/{user.Id.Value}")
        {
            Content = content,
        };
        // Set explicitly, so TestSession doesn't add its own.
        request.Headers.Add(AntiforgeryConfigurations.HeaderName, token ?? "");
        return request;
    }

    private static JsonContent RenameBody() =>
        JsonContent.Create(new { name = "Renamed By Another Site" });

    private static async Task<string> NameOf(TestSession session) =>
        (await session.Client.GetAndDeserializeAsync<ApiEndpoints.UserMe.MeBody>("/users/me")).Name;

    [Fact]
    public async Task AcceptsASignedInRequestWithTheToken()
    {
        var (user, session) = await factory.CreateSignedInSessionAsync();

        var response = await session.Client.SendAsync(
            Rename(user, RenameBody(), session.AntiforgeryToken),
            Cancellation
        );

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RefusesASignedInRequestWithoutTheToken()
    {
        var (user, session) = await factory.CreateSignedInSessionAsync();

        var response = await session.Client.SendAsync(
            Rename(user, RenameBody(), null),
            Cancellation
        );

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        (await NameOf(session)).ShouldBe(user.Name.Value);
    }

    [Fact]
    public async Task RefusesAFormPostWithoutTheToken()
    {
        // What a page on a sibling subdomain (same site, so SameSite doesn't stop it) could send.
        var (user, session) = await factory.CreateSignedInSessionAsync();

        var response = await session.Client.SendAsync(
            Rename(
                user,
                new StringContent(
                    "name=Renamed+By+Another+Site",
                    Encoding.UTF8,
                    "application/x-www-form-urlencoded"
                ),
                null
            ),
            Cancellation
        );

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await NameOf(session)).ShouldBe(user.Name.Value);
    }

    [Fact]
    public async Task RefusesAnotherUsersToken()
    {
        var (user, session) = await factory.CreateSignedInSessionAsync();
        var (_, other) = await factory.CreateSignedInSessionAsync();

        var response = await session.Client.SendAsync(
            Rename(user, RenameBody(), other.AntiforgeryToken),
            Cancellation
        );

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RefusesATokenFetchedBeforeSigningIn()
    {
        // Tokens are bound to the user: the SPA fetches a new one after signing in.
        var user = await factory.CreateUserAsync();
        var session = new TestSession(factory);
        await session.FetchAntiforgeryTokenAsync();
        var anonymousToken = session.AntiforgeryToken;
        await session.LoginAsync(user.Email.Value, fetchAntiforgeryToken: false);

        var response = await session.Client.SendAsync(
            Rename(user, RenameBody(), anonymousToken),
            Cancellation
        );

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DoesNotCheckAnonymousRequests()
    {
        // No session, nothing to forge: signing in needs no token.
        var user = await factory.CreateUserAsync();

        var response = await new TestSession(factory).LoginAsync(
            user.Email.Value,
            fetchAntiforgeryToken: false
        );

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task IssuesAReadableTokenCookie()
    {
        var session = new TestSession(factory);

        await session.FetchAntiforgeryTokenAsync();

        var cookie = session.LastSetCookies.Single(header =>
            header.StartsWith($"{AntiforgeryConfigurations.RequestTokenCookieName}=")
        );
        cookie.ShouldNotContain("httponly", Case.Insensitive);
        cookie.ShouldContain("samesite=strict", Case.Insensitive);
        cookie.ShouldContain("secure", Case.Insensitive);
        session.LastSetCookies.ShouldContain(header =>
            header.StartsWith("apptemplate_antiforgery=")
            && header.Contains("httponly", StringComparison.OrdinalIgnoreCase)
        );
    }
}
