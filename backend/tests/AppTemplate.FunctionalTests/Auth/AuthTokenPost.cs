using AppTemplate.Web.Endpoints.Authentication;

namespace AppTemplate.FunctionalTests.Auth;

[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class AuthTokenPost(CustomWebApplicationFactory<Program> factory)
    : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private Task<HttpResponseMessage> PostAsync(string email, string password) =>
        factory
            .CreateClient()
            .PostAsync(
                "/auth/token",
                JsonContent.Create(new { email, password }),
                TestContext.Current.CancellationToken
            );

    [Fact]
    public async Task ReturnsABearerTokenWithoutSettingACookie()
    {
        var user = await factory.CreateUserAsync();

        var response = await PostAsync(
            user.Email.Value,
            CustomWebApplicationFactory<Program>.Password
        );

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.Contains("Set-Cookie").ShouldBeFalse();
        var body = await response.Content.ReadFromJsonAsync<TokenResponse>(
            TestContext.Current.CancellationToken
        );
        body.ShouldNotBeNull();
        body.TokenType.ShouldBe("Bearer");
        body.AccessToken.Split('.').Length.ShouldBe(3); // a signed JWT: header.payload.signature
        body.ExpiresAt.ShouldBe(factory.Time.GetUtcNow().AddMinutes(15), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ReturnsUnauthorizedGivenAWrongPassword()
    {
        var user = await factory.CreateUserAsync();

        (await PostAsync(user.Email.Value, "wrong-password")).StatusCode.ShouldBe(
            HttpStatusCode.Unauthorized
        );
    }

    [Fact]
    public async Task ReturnsUnauthorizedForAnUnknownEmail()
    {
        (await PostAsync("nobody@example.com", "any-password")).StatusCode.ShouldBe(
            HttpStatusCode.Unauthorized
        );
    }

    [Fact]
    public async Task ReturnsForbiddenForAnUnconfirmedEmail()
    {
        var session = new TestSession(factory);
        var email = $"user-{Guid.NewGuid():N}@example.com";
        (await session.RegisterAsync(email)).StatusCode.ShouldBe(HttpStatusCode.Created);

        (await PostAsync(email, CustomWebApplicationFactory<Program>.Password)).StatusCode.ShouldBe(
            HttpStatusCode.Forbidden
        );
    }

    [Fact]
    public async Task ReturnsBadRequestGivenAnInvalidEmail()
    {
        (await PostAsync("not-an-email", "any-password")).StatusCode.ShouldBe(
            HttpStatusCode.BadRequest
        );
    }
}
