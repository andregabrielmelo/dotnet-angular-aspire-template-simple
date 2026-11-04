namespace AppTemplate.FunctionalTests.ApiEndpoints;

[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class AuthResetPasswordPost(CustomWebApplicationFactory<Program> factory)
    : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private const string NewPassword = "an entirely new passphrase";

    private async Task<IReadOnlyDictionary<string, string>> ResetLinkFor(TestUser user)
    {
        await factory
            .CreateClient()
            .PostAsJsonAsync(
                "/auth/forgot-password",
                new { email = user.Email.Value },
                TestContext.Current.CancellationToken
            );
        return factory.EmailSender.LastLinkQueryTo(user.Email.Value);
    }

    private Task<HttpResponseMessage> Reset(string email, string code, string newPassword) =>
        factory
            .CreateClient()
            .PostAsJsonAsync(
                "/auth/reset-password",
                new
                {
                    email,
                    code,
                    newPassword,
                },
                TestContext.Current.CancellationToken
            );

    [Fact]
    public async Task SetsTheNewPasswordAndEndsEverySession()
    {
        var (user, session) = await factory.CreateSignedInSessionAsync();
        var link = await ResetLinkFor(user);

        (await Reset(link["email"], link["code"], NewPassword)).StatusCode.ShouldBe(
            HttpStatusCode.NoContent
        );

        (await session.RefreshAsync()).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await new TestSession(factory).LoginAsync(user.Email.Value)).StatusCode.ShouldBe(
            HttpStatusCode.Unauthorized
        );
        (
            await new TestSession(factory).LoginAsync(user.Email.Value, NewPassword)
        ).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReturnsBadRequestGivenAWrongCode()
    {
        var user = await factory.CreateUserAsync();

        var response = await Reset(user.Email.Value, "bm90LXRoZS1jb2Rl", NewPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ReturnsBadRequestGivenAShortPassword()
    {
        var user = await factory.CreateUserAsync();
        var link = await ResetLinkFor(user);

        (await Reset(link["email"], link["code"], "short")).StatusCode.ShouldBe(
            HttpStatusCode.BadRequest
        );
    }
}
