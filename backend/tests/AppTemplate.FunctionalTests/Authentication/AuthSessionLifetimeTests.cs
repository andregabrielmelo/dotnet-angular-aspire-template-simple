using System.Net;
using Xunit;

namespace AppTemplate.FunctionalTests.Authentication;

/// <summary>
/// Refresh token lifetimes and reuse detection, by moving the session clock
/// (<see cref="AppTemplateWebApplicationFactory.Time"/>). Its own host, so the clock moves for
/// these tests only; none of them uses an access token.
/// </summary>
[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class AuthSessionLifetimeTests(AuthSessionLifetimeTests.Factory factory)
    : IClassFixture<AuthSessionLifetimeTests.Factory>
{
    private async Task<BrowserSession> SignedInAsync(string? email = null)
    {
        email ??= (await factory.CreateUserAsync()).Email.Value;
        var session = new BrowserSession(factory);
        Assert.Equal(HttpStatusCode.OK, (await session.LoginAsync(email)).StatusCode);
        return session;
    }

    [Fact]
    public async Task ReplayingARotatedToken_WithinTheGracePeriod_IsAConflict_NotReuse()
    {
        var session = await SignedInAsync();
        var original = session.RefreshToken;
        await session.RefreshAsync();
        var current = session.RefreshToken;
        factory.Time.Advance(TimeSpan.FromSeconds(5));

        session.RefreshToken = original;
        Assert.Equal(HttpStatusCode.Conflict, (await session.RefreshAsync()).StatusCode);

        session.RefreshToken = current;
        Assert.Equal(HttpStatusCode.OK, (await session.RefreshAsync()).StatusCode);
    }

    [Fact]
    public async Task ReplayingARotatedToken_Later_RevokesItsFamily_ButNotOtherSessions()
    {
        var user = await factory.CreateUserAsync();
        var stolen = await SignedInAsync(user.Email.Value);
        var otherDevice = await SignedInAsync(user.Email.Value);
        var original = stolen.RefreshToken;
        await stolen.RefreshAsync();
        var current = stolen.RefreshToken;
        factory.Time.Advance(TimeSpan.FromMinutes(1));

        stolen.RefreshToken = original;
        Assert.Equal(HttpStatusCode.Unauthorized, (await stolen.RefreshAsync()).StatusCode);

        // The legitimate holder of the family's newest token is signed out too...
        stolen.RefreshToken = current;
        Assert.Equal(HttpStatusCode.Unauthorized, (await stolen.RefreshAsync()).StatusCode);
        // ...but the same user's session on another device is a different family.
        Assert.Equal(HttpStatusCode.OK, (await otherDevice.RefreshAsync()).StatusCode);
    }

    [Fact]
    public async Task ARefreshTokenUnusedForSevenDays_HasExpired()
    {
        var session = await SignedInAsync();

        factory.Time.Advance(TimeSpan.FromDays(7));

        Assert.Equal(HttpStatusCode.Unauthorized, (await session.RefreshAsync()).StatusCode);
    }

    [Fact]
    public async Task ASessionEndsAfterThirtyDays_HoweverOftenItRefreshes()
    {
        var session = await SignedInAsync();

        for (var day = 6; day < 30; day += 6)
        {
            factory.Time.Advance(TimeSpan.FromDays(6));
            Assert.Equal(HttpStatusCode.OK, (await session.RefreshAsync()).StatusCode);
        }
        factory.Time.Advance(TimeSpan.FromDays(6)); // day 30

        Assert.Equal(HttpStatusCode.Unauthorized, (await session.RefreshAsync()).StatusCode);
    }

    public sealed class Factory : AppTemplateWebApplicationFactory;
}
