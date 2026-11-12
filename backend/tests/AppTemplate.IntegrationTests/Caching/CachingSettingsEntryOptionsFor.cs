using AppTemplate.Infrastructure.Caching;
using AppTemplate.UseCases.Caching;
using Microsoft.Extensions.Configuration;

namespace AppTemplate.IntegrationTests.Caching;

/// <summary>Each cache profile's lifetimes, from the defaults and from settings. No Docker.</summary>
public class CachingSettingsEntryOptionsFor
{
    [Fact]
    public void GivesNoneTheTopLevelLifetimes()
    {
        var options = new CachingSettings().EntryOptionsFor(CacheProfile.None);

        options.Expiration.ShouldBe(TimeSpan.FromMinutes(10));
        options.LocalCacheExpiration.ShouldBe(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void GivesShortAndLongTheirDefaultLifetimes()
    {
        var settings = new CachingSettings();

        settings.EntryOptionsFor(CacheProfile.Short).Expiration.ShouldBe(TimeSpan.FromMinutes(1));
        settings
            .EntryOptionsFor(CacheProfile.Short)
            .LocalCacheExpiration.ShouldBe(TimeSpan.FromSeconds(15));
        settings.EntryOptionsFor(CacheProfile.Long).Expiration.ShouldBe(TimeSpan.FromHours(1));
        settings
            .EntryOptionsFor(CacheProfile.Long)
            .LocalCacheExpiration.ShouldBe(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void TakesAProfilesLifetimesFromSettings()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Caching:Profiles:Short:Expiration"] = "00:02:00",
                    ["Caching:Profiles:Short:LocalExpiration"] = "00:00:30",
                }
            )
            .Build();
        var settings = configuration
            .GetSection(CachingSettings.SectionName)
            .Get<CachingSettings>()!;

        var options = settings.EntryOptionsFor(CacheProfile.Short);

        options.Expiration.ShouldBe(TimeSpan.FromMinutes(2));
        options.LocalCacheExpiration.ShouldBe(TimeSpan.FromSeconds(30));
        settings.EntryOptionsFor(CacheProfile.Long).Expiration.ShouldBe(TimeSpan.FromHours(1));
    }
}
