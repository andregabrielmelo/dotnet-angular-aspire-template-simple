using AppTemplate.Tests;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace AppTemplate.AspireTests.EndToEnd;

/// <summary>
/// A critical journey through the real browser, the Angular app, its proxy and the API: what no
/// backend test can see (the cookie and antiforgery token in a browser, the route guards). Keep
/// browser tests to journeys like this; rules and their combinations belong in the unit and
/// functional tests (ADR 006).
/// </summary>
[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class SignIn(AppHostFixture app)
{
    // The development admin (SeedData), seeded because the AppHost runs in Development.
    private const string AdminEmail = "admin@example.com";
    private const string AdminPassword = "Admin123!";

    [Fact]
    public async Task AdminSignsInAndSeesTheUsers()
    {
        await using var context = await app.NewBrowserContextAsync();
        var page = await context.NewPageAsync();
        try
        {
            await page.GotoAsync("/auth/login");
            await page.GetByLabel("Email").FillAsync(AdminEmail);
            await page.GetByLabel("Password").FillAsync(AdminPassword);
            await page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync();

            // Signed in: the session cookie is set and the app leaves the login page.
            await Expect(page)
                .Not.ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/auth/login"));

            // The users page needs users:read (permissionGuard), and its list comes from the API.
            await page.GotoAsync("/users");
            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Users" }))
                .ToBeVisibleAsync();
            await Expect(page.GetByRole(AriaRole.Listitem).Filter(new() { HasText = "· Admin" }))
                .ToBeVisibleAsync();
        }
        catch
        {
            await app.SaveFailureArtifactsAsync(context, page, nameof(AdminSignsInAndSeesTheUsers));
            throw;
        }
    }
}
