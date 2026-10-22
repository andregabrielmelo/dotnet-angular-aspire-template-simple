using System.Net;
using System.Net.Http.Json;
using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.ValueObjects;
using AppTemplate.ServiceDefaults.Logging;
using AppTemplate.UseCases.Authorization;
using AppTemplate.UseCases.Users.Update;
using AppTemplate.Web.Features.UserFeatures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AppTemplate.FunctionalTests.Logging;

/// <summary>
/// Emails, phone numbers and tokens never reach a log sink in clear text, wherever they
/// appear in an event: as a message template value, as a structured (scope) property, or
/// nested inside a destructured object. Both sinks are checked on their serialized output: the
/// console text and the OTLP protobuf bytes sent to the collector.
/// </summary>
[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class LogRedactionTests(LogRedactionTests.CapturingFactory factory)
    : IClassFixture<LogRedactionTests.CapturingFactory>
{
    // Assembled at runtime so the source holds no JWT-shaped literal for secret scanners.
    private static readonly string Jwt = string.Join(
        '.',
        "eyJhbGciOiJIUzI1NiJ9",
        "eyJzdWIiOiJsb2ctcmVkYWN0aW9uIn0",
        "c2lnbmF0dXJlLXZhbHVl"
    );

    private static string NewMarker() => $"marker-{Guid.NewGuid():N}";

    private ILogger Logger =>
        factory.Services.GetRequiredService<ILoggerFactory>().CreateLogger("LogRedactionTests");

    private void AssertAbsentFromBothSinks(params string[] secrets)
    {
        foreach (var secret in secrets)
        {
            Assert.DoesNotContain(secret, factory.Logs.Console);
            Assert.DoesNotContain(secret, factory.Logs.Otlp);
        }
    }

    private void AssertRedactedInBothSinks()
    {
        Assert.Contains(PlaceholderRedactor.Placeholder, factory.Logs.Console);
        Assert.Contains(PlaceholderRedactor.Placeholder, factory.Logs.Otlp);
    }

    [Fact]
    public async Task MessageTemplateValues_AreRedacted()
    {
        var marker = NewMarker();

        Logger.LogInformation(
            "Reset for {Email} with {AccessToken}, then {Note} ({Marker})",
            "ada.template@example.com",
            Jwt,
            "call grace.template@example.com with Bearer opaque-template-token",
            marker
        );
        await factory.Logs.WaitForAsync(marker);

        AssertAbsentFromBothSinks(
            "ada.template@example.com",
            "grace.template@example.com",
            Jwt,
            "opaque-template-token"
        );
        AssertRedactedInBothSinks();
    }

    [Fact]
    public async Task StructuredProperties_AreRedacted()
    {
        var marker = NewMarker();

        using (
            Logger.BeginScope(
                new Dictionary<string, object>
                {
                    ["PhoneNumber"] = "+55 11 98765-4321",
                    ["RefreshToken"] = "opaque-refresh-token-scope",
                    ["Recipient"] = "katherine.scope@example.com",
                }
            )
        )
        {
            Logger.LogInformation("Scoped event ({Marker})", marker);
        }
        await factory.Logs.WaitForAsync(marker);

        AssertAbsentFromBothSinks(
            "98765-4321",
            "opaque-refresh-token-scope",
            "katherine.scope@example.com"
        );
        AssertRedactedInBothSinks();
    }

    [Fact]
    public async Task DestructuredNestedObjects_AreRedacted()
    {
        var marker = NewMarker();
        var user = new
        {
            Name = "Ada",
            Contact = new
            {
                Email = new EmailAddress("ada.nested@example.com"),
                Phone = new PhoneNumber("+55", "11912345678", "42"),
            },
            Command = new UpdateUserCommand(
                UserId.From(7),
                UserName.From("Ada"),
                "11955554444",
                "+55",
                null
            ),
        };

        Logger.LogInformation("Handling {@User} ({Marker})", user, marker);
        // Without @, Serilog captures ToString(): the property name has to say what it is.
        Logger.LogInformation(
            "Plain {Phone} ({Marker})",
            new PhoneNumber("+1", "5550199999", null),
            marker
        );
        await factory.Logs.WaitForAsync(marker, occurrences: 2);

        AssertAbsentFromBothSinks(
            "ada.nested@example.com",
            "11912345678",
            "11955554444",
            "5550199999"
        );
        AssertRedactedInBothSinks();
        // Unclassified data is still logged.
        Assert.Contains("Ada", factory.Logs.Console);
    }

    [Fact]
    public async Task RequestLogs_HaveTheCallerAndEndpointButNoHeadersOrBody()
    {
        var subject = $"sub-{Guid.NewGuid():N}";
        var me = await factory
            .CreateAuthenticatedClient(subject)
            .GetFromJsonAsync<CurrentUserResponse>("/users/me");
        var client = factory.CreateAuthenticatedClient(subject, Permission.UsersWrite);
        var request = new HttpRequestMessage(HttpMethod.Put, $"/users/{me!.Id}")
        {
            Content = JsonContent.Create(
                new
                {
                    id = me.Id,
                    name = "Body Name 7f3a",
                    phoneNumber = "11977776666",
                    phoneCountryCode = "+55",
                    password = "body-password-9d2c",
                }
            ),
        };
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer header-token-5b1e");
        request.Headers.TryAddWithoutValidation("Cookie", "session=cookie-value-3e8f");

        var response = await client.SendAsync(request);
        await factory.Logs.WaitForAsync($"/users/{me.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var requestLine = factory
            .Logs.Console.Split('\n')
            .Single(line => line.Contains($"HTTP PUT /users/{me.Id} responded 200"));
        Assert.Contains(subject, requestLine);
        Assert.Contains("EndpointName", requestLine);
        Assert.Matches("[0-9a-f]{32} [0-9a-f]{16}", requestLine); // trace and span ids
        // LoggingBehavior destructures the command ({@Request}); its phone is [PersonalData].
        Assert.Contains("Handling UpdateUserCommand", factory.Logs.Console);
        AssertAbsentFromBothSinks(
            "header-token-5b1e",
            "cookie-value-3e8f",
            "body-password-9d2c",
            "11977776666"
        );
    }

    public sealed class CapturingFactory : AppTemplateWebApplicationFactory
    {
        public LogCapture Logs { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(Logs.Configure);
        }
    }
}
