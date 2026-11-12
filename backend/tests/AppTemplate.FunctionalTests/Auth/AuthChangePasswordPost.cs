namespace AppTemplate.FunctionalTests.Auth;

[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class AuthChangePasswordPost(CustomWebApplicationFactory<Program> factory)
    : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private const string NewPassword = "an entirely new passphrase";

    [Fact]
    public async Task ChangesThePasswordAndKeepsThisSessionSignedIn()
    {
        var (user, session) = await factory.CreateSignedInSessionAsync();

        var response = await session.ChangePasswordAsync(
            CustomWebApplicationFactory<Program>.Password,
            NewPassword
        );

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        session.LastSetCookies.ShouldContain(header =>
            header.StartsWith($"{TestSession.SessionCookieName}=")
        );
        (
            await session.Client.GetAsync("/users/me", TestContext.Current.CancellationToken)
        ).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await new TestSession(factory).LoginAsync(user.Email.Value)).StatusCode.ShouldBe(
            HttpStatusCode.Unauthorized
        );
        (
            await new TestSession(factory).LoginAsync(user.Email.Value, NewPassword)
        ).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task FailsGivenAWrongCurrentPassword()
    {
        var (user, session) = await factory.CreateSignedInSessionAsync();

        var response = await session.ChangePasswordAsync("not my password", NewPassword);

        // Identity's errors map to a generic error (IdentityResultExtensions).
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        (
            await new TestSession(factory).LoginAsync(user.Email.Value, NewPassword)
        ).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ReturnsUnauthorizedWhenNotSignedIn()
    {
        var response = await new TestSession(factory).ChangePasswordAsync(
            CustomWebApplicationFactory<Program>.Password,
            NewPassword
        );

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
