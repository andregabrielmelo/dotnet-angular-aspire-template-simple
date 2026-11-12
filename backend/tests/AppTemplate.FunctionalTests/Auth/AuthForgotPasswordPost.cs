namespace AppTemplate.FunctionalTests.Auth;

[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class AuthForgotPasswordPost(CustomWebApplicationFactory<Program> factory)
    : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private Task<HttpResponseMessage> Forgot(string email) =>
        factory
            .CreateClient()
            .PostAsJsonAsync(
                "/auth/forgot-password",
                new { email },
                TestContext.Current.CancellationToken
            );

    [Fact]
    public async Task EmailsAResetLinkToAConfirmedAccount()
    {
        var user = await factory.CreateUserAsync();

        (await Forgot(user.Email.Value)).StatusCode.ShouldBe(HttpStatusCode.Accepted);

        var link = factory.EmailSender.LastLinkQueryTo(user.Email.Value);
        link["email"].ShouldBe(user.Email.Value);
        link["code"].ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task AnswersTheSameForAnUnknownEmailAndSendsNothing()
    {
        var email = $"unknown-{Guid.NewGuid():N}@example.com";

        (await Forgot(email)).StatusCode.ShouldBe(HttpStatusCode.Accepted);

        factory.EmailSender.Sent.ShouldNotContain(sent => sent.To == email);
    }
}
