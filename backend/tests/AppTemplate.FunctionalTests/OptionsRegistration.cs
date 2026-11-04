using AppTemplate.Infrastructure.Email;
using AppTemplate.Infrastructure.Identity;
using AppTemplate.Web.Configurations;
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
        [typeof(MailserverConfiguration), typeof(JwtSettings), typeof(AccountLinkSettings)];

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

/// <summary>A missing or invalid required setting stops startup, naming the setting. No Docker.</summary>
public class OptionsValidationAtStartup
{
    [Theory]
    [InlineData("Jwt:SigningKey", "", "Jwt:SigningKey")]
    [InlineData("Jwt:SigningKey", "too-short", "Jwt:SigningKey")]
    [InlineData("Jwt:Issuer", "", "Issuer")]
    [InlineData("Jwt:RefreshTokenValidity", "00:01:00", "validities")]
    [InlineData("Mailserver:FromAddress", "", "FromAddress")]
    [InlineData("AccountLinks:ClientUrl", "", "AccountLinks:ClientUrl")]
    [InlineData("AccountLinks:ClientUrl", "/relative", "AccountLinks:ClientUrl")]
    public void FailsGivenAnInvalidSetting(string key, string value, string named)
    {
        using var factory = new MisconfiguredFactory(key, value);

        var exception = Should.Throw<Exception>(() => factory.Services);

        Flatten(exception).ShouldContain(named);
    }

    private static string Flatten(Exception exception) =>
        exception.InnerException is null
            ? exception.ToString()
            : exception + Flatten(exception.InnerException);

    /// <summary>The real host with one setting changed; it fails before touching the database.</summary>
    private sealed class MisconfiguredFactory : CustomWebApplicationFactory<Program>
    {
        private readonly string _key;
        private readonly string _value;

        public MisconfiguredFactory(string key, string value)
        {
            _key = key;
            _value = value;
            ConnectionString = "Host=127.0.0.1;Port=1;Database=unused";
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting(_key, _value);
        }
    }
}
