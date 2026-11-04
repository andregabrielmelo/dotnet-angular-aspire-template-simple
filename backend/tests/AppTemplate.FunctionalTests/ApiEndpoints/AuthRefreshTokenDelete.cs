namespace AppTemplate.FunctionalTests.ApiEndpoints;

/// <summary>Signing out: DELETE /auth/refresh-token.</summary>
[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class AuthRefreshTokenDelete(CustomWebApplicationFactory<Program> factory)
    : IClassFixture<CustomWebApplicationFactory<Program>>
{
    [Fact]
    public async Task RevokesTheAccessTokenBeforeItExpires()
    {
        var (_, session) = await factory.CreateSignedInSessionAsync();

        var response = await session.LogoutAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        // Same access token, still within its 15 minutes: rejected by RevokedTokenChecker.
        await session.Client.GetAndEnsureUnauthorizedAsync("/users/me");
    }

    [Fact]
    public async Task DeletesTheRefreshTokenAndItsCookie()
    {
        var (_, session) = await factory.CreateSignedInSessionAsync();
        var refreshToken = session.RefreshCookie!.Value;

        await session.LogoutAsync();

        session.RefreshCookie.ShouldBeNull();
        (await session.RefreshAsync(refreshToken)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task LeavesTheUsersOtherSessionsAlone()
    {
        var user = await factory.CreateUserAsync();
        var laptop = new TestSession(factory);
        var phone = new TestSession(factory);
        await laptop.LoginAsync(user.Email.Value);
        await phone.LoginAsync(user.Email.Value);

        await laptop.LogoutAsync();

        (await phone.Client.GetAsync("/users/me")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await phone.RefreshAsync()).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RequiresAnAccessToken()
    {
        var response = await new TestSession(factory).LogoutAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
