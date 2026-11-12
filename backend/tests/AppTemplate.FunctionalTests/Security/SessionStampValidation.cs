using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace AppTemplate.FunctionalTests.Security;

/// <summary>
/// Identity revalidates each session's security stamp every
/// <see cref="SecurityStampValidatorOptions.ValidationInterval"/> (Identity's default): a password change
/// or reset ends the other sessions then, and a role change reaches them then. Its own factory,
/// because it moves the clock.
/// </summary>
public sealed class SessionClockFactory : CustomWebApplicationFactory<Program>;

[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class SessionStampValidation(SessionClockFactory factory)
    : IClassFixture<SessionClockFactory>
{
    private const string NewPassword = "an entirely new passphrase";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private void PassTheNextStampCheck() =>
        factory.Time.Advance(
            factory
                .Services.GetRequiredService<IOptions<SecurityStampValidatorOptions>>()
                .Value.ValidationInterval + TimeSpan.FromMinutes(1)
        );

    private static async Task<HttpStatusCode> Me(TestSession session) =>
        (await session.Client.GetAsync("/users/me", Cancellation)).StatusCode;

    private async Task<TestSession> SignInAsync(TestUser user, string? password = null)
    {
        var session = new TestSession(factory);
        (
            await session.LoginAsync(
                user.Email.Value,
                password ?? CustomWebApplicationFactory<Program>.Password
            )
        ).EnsureSuccessStatusCode();
        return session;
    }

    [Fact]
    public async Task APasswordChangeEndsTheOtherSessionsButNotThisOne()
    {
        var user = await factory.CreateUserAsync();
        var laptop = await SignInAsync(user);
        var phone = await SignInAsync(user);

        (
            await laptop.ChangePasswordAsync(
                CustomWebApplicationFactory<Program>.Password,
                NewPassword
            )
        ).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        PassTheNextStampCheck();

        (await Me(phone)).ShouldBe(HttpStatusCode.Unauthorized);
        (await Me(laptop)).ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task APasswordResetEndsEverySession()
    {
        var user = await factory.CreateUserAsync();
        var session = await SignInAsync(user);
        await factory
            .CreateClient()
            .PostAsJsonAsync(
                "/auth/forgot-password",
                new { email = user.Email.Value },
                Cancellation
            );
        var link = factory.EmailSender.LastLinkQueryTo(user.Email.Value);
        (
            await factory
                .CreateClient()
                .PostAsJsonAsync(
                    "/auth/reset-password",
                    new
                    {
                        email = link["email"],
                        code = link["code"],
                        newPassword = NewPassword,
                    },
                    Cancellation
                )
        ).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        PassTheNextStampCheck();

        (await Me(session)).ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ARoleChangeReachesTheSessionAtTheNextStampCheck()
    {
        var user = await factory.CreateUserAsync();
        var session = await SignInAsync(user);
        await factory.AddToRoleAsync(user, Role.Admin);

        (await session.Client.GetAsync("/users", Cancellation)).StatusCode.ShouldBe(
            HttpStatusCode.Forbidden
        );
        PassTheNextStampCheck();

        (await session.Client.GetAsync("/users", Cancellation)).StatusCode.ShouldBe(
            HttpStatusCode.OK
        );
    }
}
