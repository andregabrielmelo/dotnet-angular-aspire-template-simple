using System.Net.Http.Headers;
using AppTemplate.Web.Endpoints.Authentication;
using AppTemplate.Web.Endpoints.Users;

namespace AppTemplate.FunctionalTests.Security;

/// <summary>
/// A bearer token from <c>POST /auth/token</c> authenticates like the session cookie (ADR 018):
/// the same user and permissions, without antiforgery, until it expires.
/// </summary>
[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class BearerAuthentication(CustomWebApplicationFactory<Program> factory)
    : IClassFixture<CustomWebApplicationFactory<Program>>
{
    /// <summary>A client with no cookies that sends <paramref name="user"/>'s bearer token.</summary>
    private async Task<HttpClient> BearerClientAsync(TestUser user)
    {
        var client = factory.CreateClient(new() { HandleCookies = false });
        var response = await client.PostAsync(
            "/auth/token",
            JsonContent.Create(
                new
                {
                    email = user.Email.Value,
                    password = CustomWebApplicationFactory<Program>.Password,
                }
            ),
            TestContext.Current.CancellationToken
        );
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(
            TestContext.Current.CancellationToken
        );
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token!.AccessToken
        );
        return client;
    }

    [Fact]
    public async Task AuthenticatesTheTokensUser()
    {
        var user = await factory.CreateUserAsync();
        using var client = await BearerClientAsync(user);

        var me = await client.GetAndDeserializeAsync<User.UserMe.MeBody>("/users/me");

        me.Id.ShouldBe(user.Id.Value);
        me.Email.ShouldBe(user.Email.Value);
    }

    [Fact]
    public async Task AcceptsUnsafeRequestsWithoutAnAntiforgeryToken()
    {
        var user = await factory.CreateUserAsync();
        using var client = await BearerClientAsync(user);

        var response = await client.PutAndDeserializeAsync<UpdateUserResponse>(
            $"/users/{user.Id.Value}",
            JsonContent.Create(new { name = "Renamed By Token" })
        );

        response.User.Name.ShouldBe("Renamed By Token");
    }

    [Fact]
    public async Task CarriesThePermissionsOfTheUsersRoles()
    {
        using var member = await BearerClientAsync(await factory.CreateUserAsync());
        using var admin = await BearerClientAsync(await factory.CreateUserAsync(Role.Admin));

        (
            await member.GetAsync("/users", TestContext.Current.CancellationToken)
        ).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await admin.GetAsync("/users", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(
            HttpStatusCode.OK
        );
    }

    [Fact]
    public async Task RefusesATokenSignedWithAnotherKey()
    {
        var user = await factory.CreateUserAsync();
        using var client = await BearerClientAsync(user);
        var parts = client.DefaultRequestHeaders.Authorization!.Parameter!.Split('.');
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            $"{parts[0]}.{parts[1]}.{new string('A', parts[2].Length)}"
        );

        await client.GetAndEnsureUnauthorizedAsync("/users/me");
    }

    [Fact]
    public async Task RefusesAnExpiredToken()
    {
        var user = await factory.CreateUserAsync();
        using var client = await BearerClientAsync(user);

        factory.Time.Advance(TimeSpan.FromMinutes(16)); // past the 15-minute lifetime

        await client.GetAndEnsureUnauthorizedAsync("/users/me");
    }
}
