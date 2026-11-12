using AppTemplate.FunctionalTests.User;

namespace AppTemplate.FunctionalTests.Auth;

[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class AuthRegisterPost(CustomWebApplicationFactory<Program> factory)
    : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private static string NewEmail() => $"register-{Guid.NewGuid():N}@example.com";

    [Fact]
    public async Task CreatesTheAccountAndEmailsAConfirmationLinkButDoesNotSignIn()
    {
        var email = NewEmail();
        var session = new TestSession(factory);

        var response = await session.RegisterAsync(email);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        session.SessionCookie.ShouldBeNull();
        factory.EmailSender.Sent.ShouldContain(sent =>
            sent.To == email && sent.Subject == "Confirm your email"
        );
    }

    [Fact]
    public async Task TheAccountCanSignInOnlyOnceItsEmailIsConfirmed()
    {
        var email = NewEmail();
        var session = new TestSession(factory);
        await session.RegisterAsync(email);

        (await session.LoginAsync(email)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var link = factory.EmailSender.LastLinkQueryTo(email);
        (
            await session.Client.PostAsync(
                "/auth/confirm-email",
                JsonContent.Create(new { userId = int.Parse(link["userId"]), code = link["code"] })
            )
        ).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await session.LoginAsync(email)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var me = await session.Client.GetAndDeserializeAsync<UserMe.MeBody>("/users/me");
        me.Email.ShouldBe(email);
    }

    [Fact]
    public async Task FailsGivenATakenEmailInAnyCase()
    {
        var email = NewEmail();
        await new TestSession(factory).RegisterAsync(email);

        var response = await new TestSession(factory).RegisterAsync(email.ToUpperInvariant());

        // Identity's errors map to a generic error (IdentityResultExtensions).
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
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
