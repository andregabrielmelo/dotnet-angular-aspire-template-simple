namespace AppTemplate.FunctionalTests.Security;

/// <summary>Identity's session cookie, as an API uses it (ADR 015).</summary>
[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class SessionCookie(CustomWebApplicationFactory<Program> factory)
    : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task LogoutDeletesTheCookieAndEndsTheSession()
    {
        var (_, session) = await factory.CreateSignedInSessionAsync();

        (await session.LogoutAsync()).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        session.SessionCookie.ShouldBeNull();
        (await session.Client.GetAsync("/users/me", Cancellation)).StatusCode.ShouldBe(
            HttpStatusCode.Unauthorized
        );
    }

    [Fact]
    public async Task LogoutNeedsASession()
    {
        (await new TestSession(factory).LogoutAsync()).StatusCode.ShouldBe(
            HttpStatusCode.Unauthorized
        );
    }

    [Fact]
    public async Task RefusesATamperedCookie()
    {
        var session = new TestSession(factory);
        session.SetSessionCookie("CfDJ8-not-a-protected-ticket");

        (await session.Client.GetAsync("/users/me", Cancellation)).StatusCode.ShouldBe(
            HttpStatusCode.Unauthorized
        );
    }

    [Fact]
    public async Task AnswersUnauthorizedInsteadOfRedirectingToALoginPage()
    {
        var response = await new TestSession(factory).Client.GetAsync("/users/me", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.Location.ShouldBeNull();
    }

    [Fact]
    public async Task AnswersForbiddenInsteadOfRedirectingToAnAccessDeniedPage()
    {
        var (_, member) = await factory.CreateSignedInSessionAsync();

        var response = await member.Client.GetAsync("/users", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        response.Headers.Location.ShouldBeNull();
    }
}
