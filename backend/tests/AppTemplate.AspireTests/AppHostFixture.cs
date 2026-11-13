using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Microsoft.Playwright;
using Xunit;

[assembly: AssemblyFixture(typeof(AppTemplate.AspireTests.AppHostFixture))]

namespace AppTemplate.AspireTests;

/// <summary>
/// The whole local composition, started once per test run and shared by every test here: the
/// real AppHost (Postgres, Redis, Mailpit, the API and the Angular dev server) with throwaway
/// containers (<see cref="ThrowawayContainers"/>). One AppHost per run, because two would run
/// <c>npm ci</c> in the same <c>frontend/</c> folder at once. Aspire testing runs the AppHost in
/// Development, so the API seeds its demo accounts (<c>SeedData</c>). The browser tests also get
/// a headless Chromium (<see cref="NewBrowserContextAsync"/>), launched on first use. Needs
/// Docker, like the integration tests.
/// </summary>
public sealed class AppHostFixture : IAsyncLifetime
{
    // npm ci and the Angular dev server's first build take a while on a cold machine.
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(8);

    private readonly SemaphoreSlim _browserLock = new(1, 1);
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public DistributedApplication App { get; private set; } = null!;

    /// <summary>The Angular app's HTTPS address, which also serves <c>/api</c> through its proxy.</summary>
    public Uri AngularUrl { get; private set; } = null!;

    /// <summary>Traces and screenshots of failed browser tests.</summary>
    public string ArtifactsDirectory { get; } =
        Path.Combine(AppContext.BaseDirectory, "TestResults", "playwright");

    public async ValueTask InitializeAsync()
    {
        using var timeout = new CancellationTokenSource(StartupTimeout);
        var cancellationToken = timeout.Token;

        var builder =
            await DistributedApplicationTestingBuilder.CreateAsync<Projects.AppTemplate_AppHost>(
                cancellationToken
            );
        builder.UseThrowawayContainers();
        App = await builder.BuildAsync(cancellationToken);
        await App.StartAsync(cancellationToken);
        await App.ResourceNotifications.WaitForResourceHealthyAsync("web", cancellationToken);
        await App.ResourceNotifications.WaitForResourceHealthyAsync("angular", cancellationToken);
        AngularUrl = App.GetEndpoint("angular", "https");
    }

    /// <summary>
    /// A fresh browser context (its own cookies) on the Angular app, recording a trace that
    /// <see cref="SaveFailureArtifactsAsync"/> saves when the test fails.
    /// </summary>
    public async Task<IBrowserContext> NewBrowserContextAsync()
    {
        var browser = await GetBrowserAsync();
        var context = await browser.NewContextAsync(
            new()
            {
                BaseURL = AngularUrl.ToString(),
                // The app uses the ASP.NET Core development certificate. Chromium on Linux reads
                // its own certificate store (NSS), not the OpenSSL one `dotnet dev-certs` trusts.
                IgnoreHTTPSErrors = true,
            }
        );
        await context.Tracing.StartAsync(
            new()
            {
                Screenshots = true,
                Snapshots = true,
                Sources = true,
            }
        );
        return context;
    }

    /// <summary>Saves the context's trace and a screenshot of the page, named after the test.</summary>
    public async Task SaveFailureArtifactsAsync(IBrowserContext context, IPage page, string name)
    {
        Directory.CreateDirectory(ArtifactsDirectory);
        var prefix = Path.Combine(ArtifactsDirectory, $"{name}-{DateTime.UtcNow:yyyyMMdd-HHmmss}");
        await page.ScreenshotAsync(new() { Path = $"{prefix}.png", FullPage = true });
        await context.Tracing.StopAsync(new() { Path = $"{prefix}-trace.zip" });
    }

    private async Task<IBrowser> GetBrowserAsync()
    {
        await _browserLock.WaitAsync();
        try
        {
            if (_browser is null)
            {
                // Downloads Chromium the first time (into ~/.cache/ms-playwright); a no-op
                // afterwards. CI installs it beforehand, with its system libraries (--with-deps).
                var installed = Microsoft.Playwright.Program.Main(["install", "chromium"]);
                if (installed != 0)
                {
                    throw new InvalidOperationException(
                        $"Installing Chromium for Playwright failed (exit code {installed})."
                    );
                }
                _playwright = await Playwright.CreateAsync();
                _browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true });
            }
            return _browser;
        }
        finally
        {
            _browserLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.DisposeAsync();
        }
        _playwright?.Dispose();
        _browserLock.Dispose();
        if (App is not null)
        {
            await App.DisposeAsync();
        }
    }
}
