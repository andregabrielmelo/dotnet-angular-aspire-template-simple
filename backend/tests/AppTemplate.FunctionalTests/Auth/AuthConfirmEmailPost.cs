using System.Globalization;

namespace AppTemplate.FunctionalTests.Auth;

[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class AuthConfirmEmailPost(CustomWebApplicationFactory<Program> factory)
    : IClassFixture<CustomWebApplicationFactory<Program>>
{
    [Fact]
    public async Task ReturnsBadRequestForTheCodeFieldGivenAWrongCode()
    {
        var email = $"confirm-{Guid.NewGuid():N}@example.com";
        var session = new TestSession(factory);
        await session.RegisterAsync(email);
        var link = factory.EmailSender.LastLinkQueryTo(email);

        var response = await session.Client.PostAsJsonAsync(
            "/auth/confirm-email",
            new
            {
                userId = int.Parse(link["userId"], CultureInfo.InvariantCulture),
                code = "bm90LXRoZS1jb2Rl",
            },
            TestContext.Current.CancellationToken
        );

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)
        ).ShouldContain("\"code\"");
    }

    [Fact]
    public async Task ReturnsBadRequestGivenAnIncompleteLink()
    {
        var response = await factory
            .CreateClient()
            .PostAsJsonAsync(
                "/auth/confirm-email",
                new { userId = 0, code = "" },
                TestContext.Current.CancellationToken
            );

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
