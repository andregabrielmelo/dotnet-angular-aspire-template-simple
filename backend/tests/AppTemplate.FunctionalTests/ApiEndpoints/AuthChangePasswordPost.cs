namespace AppTemplate.FunctionalTests.ApiEndpoints;

[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class AuthChangePasswordPost(CustomWebApplicationFactory<Program> factory)
    : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private const string NewPassword = "an entirely new passphrase";

    [Fact]
    public async Task KeepsThisSessionAndEndsTheOthers()
    {
        var user = await factory.CreateUserAsync();
        var laptop = new TestSession(factory);
        var phone = new TestSession(factory);
        await laptop.LoginAsync(user.Email.Value);
        await phone.LoginAsync(user.Email.Value);

        var response = await laptop.ChangePasswordAsync(
            CustomWebApplicationFactory<Program>.Password,
            NewPassword
        );

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await laptop.RefreshAsync()).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await phone.RefreshAsync()).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (
            await new TestSession(factory).LoginAsync(user.Email.Value, NewPassword)
        ).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReturnsBadRequestGivenAWrongCurrentPassword()
    {
        var (_, session) = await factory.CreateSignedInSessionAsync();

        var response = await session.ChangePasswordAsync("not my password", NewPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)
        ).ShouldContain("\"currentPassword\"");
    }

    [Fact]
    public async Task ReturnsUnauthorizedWithoutAnAccessToken()
    {
        var response = await new TestSession(factory).ChangePasswordAsync(
            CustomWebApplicationFactory<Program>.Password,
            NewPassword
        );

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
