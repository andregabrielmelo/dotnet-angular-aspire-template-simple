using AppTemplate.Infrastructure.Caching;
using AppTemplate.Infrastructure.Email;
using AppTemplate.Infrastructure.Identity;
using AppTemplate.Infrastructure.Logging;
using AppTemplate.Web.Configurations;
using AppTemplate.Web.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AppTemplate.FunctionalTests;

/// <summary>
/// The Web host's options are registered and validated before use (AGENTS.md, Configuration), so
/// dropping a registration fails here instead of silently using defaults.
/// </summary>
[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class OptionsRegistration(CustomWebApplicationFactory<Program> factory)
    : IClassFixture<CustomWebApplicationFactory<Program>>
{
    public static TheoryData<Type> WebOptions() =>
        [
            typeof(MailserverConfiguration),
            typeof(AccountLinkSettings),
            typeof(RateLimitSettings),
            typeof(ForwardedHeadersSettings),
            typeof(CachingSettings),
        ];

    [Theory]
    [MemberData(nameof(WebOptions))]
    public void RegistersAndValidatesTheOptions(Type options)
    {
        factory
            .Services.GetServices(typeof(IConfigureOptions<>).MakeGenericType(options))
            .ShouldNotBeEmpty();
        factory
            .Services.GetServices(typeof(IValidateOptions<>).MakeGenericType(options))
            .ShouldNotBeEmpty();
    }
}

/// <summary>
/// A missing or invalid required setting stops startup, naming the setting. No Docker, and no
/// host: the Web host's real registration (<c>AddOptionConfigurations</c>) is checked by the
/// <see cref="IStartupValidator"/> that <c>Host.StartAsync</c> runs, which validates only options
/// registered with <c>ValidateOnStart</c>. Starting a whole host per case raced with its disposal
/// on slow runners, surfacing an ObjectDisposedException instead of the validation error.
/// </summary>
public class OptionsValidationAtStartup
{
    [Theory]
    [InlineData("Mailserver:FromAddress", "", "FromAddress")]
    [InlineData("AccountLinks:ClientUrl", "", "AccountLinks:ClientUrl")]
    [InlineData("AccountLinks:ClientUrl", "/relative", "AccountLinks:ClientUrl")]
    [InlineData("RateLimiting:AuthenticationPermitLimit", "0", "RateLimiting")]
    [InlineData("ForwardedHeaders:KnownProxies:0", "not-an-address", "KnownProxies")]
    [InlineData("Caching:LocalExpiration", "00:00:00", "Caching")]
    [InlineData("Caching:LocalExpiration", "00:20:00", "Caching")]
    [InlineData("Caching:Profiles:Short:LocalExpiration", "00:00:00", "Caching:Profiles")]
    [InlineData("Caching:Profiles:Short:LocalExpiration", "00:02:00", "Caching:Profiles")]
    public void FailsGivenAnInvalidSetting(string key, string value, string named)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    // Valid values for the required settings, so only the one under test fails.
                    ["Mailserver:FromAddress"] = "noreply@app.test",
                    ["AccountLinks:ClientUrl"] = CustomWebApplicationFactory<Program>.ClientUrl,
                    [key] = value,
                }
            )
            .Build();
        using var provider = new ServiceCollection()
            .AddOptionConfigurations(
                configuration,
                new LoggerAdapter<OptionsRegistration>(NullLoggerFactory.Instance)
            )
            .BuildServiceProvider();

        var exception = Should.Throw<OptionsValidationException>(() =>
            provider.GetRequiredService<IStartupValidator>().Validate()
        );

        exception.Message.ShouldContain(named);
    }

    [Fact]
    public void PassesGivenValidSettings()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Mailserver:FromAddress"] = "noreply@app.test",
                    ["AccountLinks:ClientUrl"] = CustomWebApplicationFactory<Program>.ClientUrl,
                }
            )
            .Build();
        using var provider = new ServiceCollection()
            .AddOptionConfigurations(
                configuration,
                new LoggerAdapter<OptionsRegistration>(NullLoggerFactory.Instance)
            )
            .BuildServiceProvider();

        Should.NotThrow(() => provider.GetRequiredService<IStartupValidator>().Validate());
    }
}
