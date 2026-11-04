using System.Net.Http.Headers;
using System.Security.Claims;
using FastEndpoints.Security;

namespace AppTemplate.FunctionalTests.ApiEndpoints;

/// <summary>Only tokens this API signed, for this API, and still valid, are accepted.</summary>
[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class AccessTokenValidation(CustomWebApplicationFactory<Program> factory)
    : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private async Task<HttpStatusCode> MeWithAsync(string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (await client.GetAsync("/users/me")).StatusCode;
    }

    private static string Token(
        int userId,
        string signingKey = CustomWebApplicationFactory<Program>.SigningKey,
        string issuer = "apptemplate",
        DateTime? expireAt = null
    ) =>
        JwtBearer.CreateToken(options =>
        {
            options.SigningKey = signingKey;
            options.Issuer = issuer;
            options.Audience = "apptemplate-api";
            options.ExpireAt = expireAt ?? DateTime.UtcNow.AddMinutes(10);
            options.User.Claims.Add(new Claim("sub", userId.ToString()));
        });

    [Fact]
    public async Task AcceptsATokenSignedByThisApi()
    {
        var user = await factory.CreateUserAsync();

        (await MeWithAsync(Token(user.Id.Value))).ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RejectsATokenSignedWithAnotherKey()
    {
        var user = await factory.CreateUserAsync();

        (
            await MeWithAsync(
                Token(user.Id.Value, signingKey: "another-key-of-at-least-thirty-two-chars")
            )
        ).ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RejectsATokenFromAnotherIssuer()
    {
        var user = await factory.CreateUserAsync();

        (await MeWithAsync(Token(user.Id.Value, issuer: "someone-else"))).ShouldBe(
            HttpStatusCode.Unauthorized
        );
    }

    [Fact]
    public async Task RejectsAnExpiredToken()
    {
        var user = await factory.CreateUserAsync();

        (
            await MeWithAsync(Token(user.Id.Value, expireAt: DateTime.UtcNow.AddMinutes(-5)))
        ).ShouldBe(HttpStatusCode.Unauthorized);
    }
}
