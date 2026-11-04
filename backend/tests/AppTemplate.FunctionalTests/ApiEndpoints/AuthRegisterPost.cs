namespace AppTemplate.FunctionalTests.ApiEndpoints;

[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class AuthRegisterPost(CustomWebApplicationFactory<Program> factory)
    : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private static string NewEmail() => $"register-{Guid.NewGuid():N}@example.com";

    [Fact]
    public async Task CreatesTheAccountSignsItInAndSendsAWelcomeEmail()
    {
        var email = NewEmail();
        var session = new TestSession(factory);

        var response = await session.RegisterAsync(email);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        session.RefreshCookie.ShouldNotBeNull();
        var me = await session.Client.GetAndDeserializeAsync<UserMe.MeBody>("/users/me");
        me.Email.ShouldBe(email);
        factory.EmailSender.Sent.ShouldContain(sent =>
            sent.To == email && sent.Subject == "Welcome!"
        );
    }

    [Fact]
    public async Task ReturnsConflictGivenATakenEmailInAnyCase()
    {
        var email = NewEmail();
        await new TestSession(factory).RegisterAsync(email);

        var response = await new TestSession(factory).RegisterAsync(email.ToUpperInvariant());

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData("not-an-email", "long enough password")]
    [InlineData("ada@example.com", "short")]
    public async Task ReturnsBadRequestGivenInvalidInput(string email, string password)
    {
        var response = await new TestSession(factory).RegisterAsync(email, password);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
