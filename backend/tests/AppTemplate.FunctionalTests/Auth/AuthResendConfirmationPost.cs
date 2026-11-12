namespace AppTemplate.FunctionalTests.Auth;

[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class AuthResendConfirmationPost(CustomWebApplicationFactory<Program> factory)
    : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private Task<HttpResponseMessage> Resend(string email) =>
        factory
            .CreateClient()
            .PostAsJsonAsync(
                "/auth/resend-confirmation",
                new { email },
                TestContext.Current.CancellationToken
            );

    [Fact]
    public async Task EmailsANewWorkingLinkToAnUnconfirmedAccount()
    {
        var email = $"resend-{Guid.NewGuid():N}@example.com";
        await new TestSession(factory).RegisterAsync(email);

        (await Resend(email)).StatusCode.ShouldBe(HttpStatusCode.Accepted);

        factory.EmailSender.Sent.Count(sent => sent.To == email).ShouldBe(2);
        var link = factory.EmailSender.LastLinkQueryTo(email);
        (
            await factory
                .CreateClient()
                .PostAsync(
                    "/auth/confirm-email",
                    JsonContent.Create(
                        new { userId = int.Parse(link["userId"]), code = link["code"] }
                    )
                )
        ).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AnswersTheSameForAnUnknownEmailAndSendsNothing()
    {
        var email = $"unknown-{Guid.NewGuid():N}@example.com";

        (await Resend(email)).StatusCode.ShouldBe(HttpStatusCode.Accepted);

        factory.EmailSender.Sent.ShouldNotContain(sent => sent.To == email);
    }
}
