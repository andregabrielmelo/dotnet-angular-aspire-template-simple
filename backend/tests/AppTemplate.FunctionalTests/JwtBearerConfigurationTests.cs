using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace AppTemplate.FunctionalTests;

/// <summary>
/// The functional tests swap JWT validation for TestAuthHandler, so check the real JwtBearer
/// setup separately: right audience, JWT claim names, and a configurable HTTPS authority.
/// </summary>
[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class JwtBearerConfigurationTests(AppTemplateWebApplicationFactory factory)
    : IClassFixture<AppTemplateWebApplicationFactory>
{
    [Fact]
    public void JwtBearer_ValidatesKeycloakTokensForThisApi()
    {
        var options = factory
            .Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        Assert.Equal("https://keycloak.test/realms/apptemplate", options.Authority);
        Assert.Equal("apptemplate-api", options.Audience);
        Assert.True(options.RequireHttpsMetadata);
        Assert.False(options.MapInboundClaims);
    }
}
