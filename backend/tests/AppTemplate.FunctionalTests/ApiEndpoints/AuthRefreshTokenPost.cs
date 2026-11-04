using System.Text.Json;
using AppTemplate.SharedKernel;

namespace AppTemplate.FunctionalTests.ApiEndpoints;

[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class AuthRefreshTokenPost(CustomWebApplicationFactory<Program> factory)
    : IClassFixture<CustomWebApplicationFactory<Program>>
{
    [Fact]
    public async Task ReturnsANewAccessTokenAndRotatesTheCookie()
    {
        var (_, session) = await factory.CreateSignedInSessionAsync();
        var firstAccessToken = session.AccessToken;
        var firstCookie = session.RefreshCookie!.Value;

        var response = await session.RefreshAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        session.AccessToken.ShouldNotBe(firstAccessToken);
        session.RefreshCookie!.Value.ShouldNotBe(firstCookie);
        (await session.Client.GetAsync("/users/me")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReturnsOnlyTheAccessTokenAndItsExpiry()
    {
        var (_, session) = await factory.CreateSignedInSessionAsync();

        var response = await session.RefreshAsync();

        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        body.EnumerateObject()
            .Select(property => property.Name)
            .Order()
            .ShouldBe(["accessToken", "accessTokenExpiry"]);
    }

    [Fact]
    public async Task RejectsARefreshTokenThatWasAlreadyUsed()
    {
        var (_, session) = await factory.CreateSignedInSessionAsync();
        var usedCookie = session.RefreshCookie!.Value;
        await session.RefreshAsync();

        var response = await session.RefreshAsync(usedCookie);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RejectsARequestWithoutTheCookie()
    {
        var response = await new TestSession(factory).RefreshAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task IgnoresARefreshTokenSentInTheBody()
    {
        var (_, session) = await factory.CreateSignedInSessionAsync();
        var cookie = session.RefreshCookie!.Value;

        // Only the cookie counts: a token in the JSON body is not read.
        var response = await new TestSession(factory).Client.PostAsJsonAsync(
            "/auth/refresh-token",
            new { refreshToken = cookie }
        );

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PutsTheUsersCurrentPermissionsInTheNewAccessToken()
    {
        var (user, session) = await factory.CreateSignedInSessionAsync();
        (await session.Client.GetAsync("/users")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        await factory.AddToRoleAsync(user, Roles.Admin);
        // The current access token keeps the old permissions until it's replaced.
        (await session.Client.GetAsync("/users")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        await session.RefreshAsync();

        (await session.Client.GetAsync("/users")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RejectsTheTokenOfADeletedUser()
    {
        var (user, session) = await factory.CreateSignedInSessionAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<IRepository<User>>();
            await users.DeleteAsync((await users.GetByIdAsync(user.Id))!);
        }

        var response = await session.RefreshAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}

/// <summary>Its own host, so moving the clock doesn't affect the tests above.</summary>
[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class AuthRefreshTokenPostAfterExpiry(CustomWebApplicationFactory<Program> factory)
    : IClassFixture<CustomWebApplicationFactory<Program>>
{
    [Fact]
    public async Task RejectsARefreshTokenPastItsValidity()
    {
        var (_, session) = await factory.CreateSignedInSessionAsync();

        factory.Time.Advance(TimeSpan.FromDays(7));
        var response = await session.RefreshAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
