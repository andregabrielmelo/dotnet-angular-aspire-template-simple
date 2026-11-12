namespace AppTemplate.FunctionalTests;

/// <summary>
/// The probes follow Aspire's ServiceDefaults: mapped in Development only (see MapDefaultEndpoints),
/// so a host in any other environment - these tests run as "Testing" - doesn't expose them. The
/// client signs in, because the fallback policy answers an anonymous request to an unmapped path
/// with 401 before routing can answer 404.
/// </summary>
[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class HealthEndpoints(CustomWebApplicationFactory<Program> factory)
    : IClassFixture<CustomWebApplicationFactory<Program>>
{
    [Theory]
    [InlineData("/alive")]
    [InlineData("/health")]
    public async Task AreNotMappedOutsideDevelopment(string path)
    {
        var (_, session) = await factory.CreateSignedInSessionAsync();

        await session.Client.GetAndEnsureNotFoundAsync(path);
    }
}
