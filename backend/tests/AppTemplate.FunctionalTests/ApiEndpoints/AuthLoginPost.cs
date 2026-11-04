using System.Text.Json;
using AppTemplate.Web.Authentication;

namespace AppTemplate.FunctionalTests.ApiEndpoints;

[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class AuthLoginPost(CustomWebApplicationFactory<Program> factory)
    : IClassFixture<CustomWebApplicationFactory<Program>>
{
    [Fact]
    public async Task ReturnsTheAccessTokenAndItsExpiryButNeverTheRefreshToken()
    {
        var user = await factory.CreateUserAsync();
        var session = new TestSession(factory);

        var response = await session.LoginAsync(user.Email.Value);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        body.EnumerateObject()
            .Select(property => property.Name)
            .Order()
            .ShouldBe(["accessToken", "accessTokenExpiry"]);
        session.AccessToken.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task SetsTheRefreshTokenAsAStrictHttpOnlyCookieForTheRefreshRoute()
    {
        var user = await factory.CreateUserAsync();
        var session = new TestSession(factory);

        await session.LoginAsync(user.Email.Value);

        var setCookie = session.LastSetCookies.Single(header =>
            header.StartsWith($"{RefreshTokenCookie.Name}=")
        );
        setCookie.ShouldContain("httponly", Case.Insensitive);
        setCookie.ShouldContain("secure", Case.Insensitive);
        setCookie.ShouldContain("samesite=strict", Case.Insensitive);
        setCookie.ShouldContain("path=/api/auth/refresh-token", Case.Insensitive);
    }

    [Fact]
    public async Task ReturnsUnauthorizedGivenAWrongPassword()
    {
        var user = await factory.CreateUserAsync();

        var response = await new TestSession(factory).LoginAsync(
            user.Email.Value,
            "wrong-password"
        );

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AnswersAnUnknownEmailLikeAWrongPassword()
    {
        var user = await factory.CreateUserAsync();

        var wrongPassword = await new TestSession(factory).LoginAsync(
            user.Email.Value,
            "wrong-password"
        );
        var unknownEmail = await new TestSession(factory).LoginAsync(
            $"nobody-{Guid.NewGuid():N}@example.com",
            "wrong-password"
        );

        unknownEmail.StatusCode.ShouldBe(wrongPassword.StatusCode);
    }

    [Fact]
    public async Task LocksTheAccountOutAfterFiveWrongPasswords()
    {
        var user = await factory.CreateUserAsync();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await new TestSession(factory).LoginAsync(user.Email.Value, "wrong-password");
        }

        var response = await new TestSession(factory).LoginAsync(user.Email.Value);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ReturnsBadRequestGivenAnInvalidEmail()
    {
        var response = await new TestSession(factory).LoginAsync("not-an-email", "any-password");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
