using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// Add Postgre SQL Server container
var postgres = builder
    .AddPostgres("postgres")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

// Add the database
var applicationDatabase = postgres.AddDatabase("apptemplate");

// Redis: HybridCache's distributed (L2) cache, shared by every API instance so cache entries and
// invalidations reach all of them (ADR 017). Persistent, with a data volume, so the container and
// its entries outlive an AppHost run; Redis Insight (its own resource) browses the keys.
var cache = builder
    .AddRedis("cache")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent)
    .WithRedisInsight();

// The API project, linked to the database and the cache. HTTPS only (its "https" launch profile),
// with the ASP.NET Core development certificate (ADR 016).
//
// What the AppHost passes the API is only what depends on this composition: connection strings
// (WithReference), the Angular app's address and the SMTP server (WithEnvironment, below), and
// outside Development the deployment's values (AddParameter). Everything else, development defaults
// included (the sender address, loopback as a known proxy), is the API's own configuration, in
// AppTemplate.Web/appsettings.Development.json.
var api = builder
    .AddProject<Projects.AppTemplate_Web>("web", launchProfileName: "https")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName)
    .WithReference(applicationDatabase)
    .WaitFor(applicationDatabase)
    .WithReference(cache)
    .WaitFor(cache)
    // Readiness (Postgres reachable): resources that WaitFor(api) start only once it's ready.
    .WithHttpHealthCheck("/health", endpointName: "https");

// The Angular dev server is the browser's single origin: its proxy (frontend/proxy.conf.mjs)
// forwards /api to the Web API, found through the services__web__https__0 variable that
// WithReference(api) sets. Same origin, so there is no CORS. Both legs are HTTPS: ng serve gets
// the development certificate's files as arguments, and Node trusts the certificate when the
// proxy connects to the API (ADR 016).
#pragma warning disable ASPIRECERTIFICATES001 // Aspire's HTTPS certificate APIs are experimental.
var frontend = builder
    .AddJavaScriptApp("angular", "../../../frontend", runScriptName: "start")
    .WithNpm(installCommand: "ci")
    .WithHttpsEndpoint(env: "PORT")
    .WithHttpsDeveloperCertificate()
    .WithHttpsCertificateConfiguration(context =>
    {
        // npm's separator: without it, npm takes --ssl* as its own options and ng serve only
        // gets the bare file paths. Aspire adds it only for its own run-script arguments.
        context.Arguments.Add("--");
        context.Arguments.Add("--ssl");
        context.Arguments.Add("--ssl-cert");
        context.Arguments.Add(context.CertificatePath);
        context.Arguments.Add("--ssl-key");
        context.Arguments.Add(context.KeyPath);
        return Task.CompletedTask;
    })
    .WithDeveloperCertificateTrust(true)
    .WithHttpHealthCheck("/", endpointName: "https")
    .WithReference(api)
    .WaitFor(api)
    .WithExternalHttpEndpoints()
    .PublishAsDockerFile();
#pragma warning restore ASPIRECERTIFICATES001

// Account emails (confirmation, password reset) link to the Angular app's pages.
api.WithEnvironment("AccountLinks__ClientUrl", frontend.GetEndpoint("https"));

if (builder.Environment.IsDevelopment())
{
    // Development SMTP catcher: the API's emails (IEmailSender, such as the welcome email) go
    // here, and can be read in Mailpit's web UI (the "mailpit" resource's http endpoint).
    var mailpit = builder.AddMailPit("mailpit", httpPort: 8025, smtpPort: 1025);

    var smtp = mailpit.GetEndpoint("smtp");
    api.WithEnvironment("Mailserver__Hostname", smtp.Property(EndpointProperty.Host))
        .WithEnvironment("Mailserver__Port", smtp.Property(EndpointProperty.Port))
        .WaitFor(mailpit);
}
else
{
    // The SMTP server the API's emails (IEmailSender) go to, and their sender: the deployment's
    // values, as parameters (Parameters:mailserver-hostname, ... in configuration, or environment
    // variables).
    var mailserverHostname = builder.AddParameter("mailserver-hostname");
    var mailserverPort = builder.AddParameter("mailserver-port");
    var mailserverFromAddress = builder.AddParameter("mailserver-from-address");

    api.WithEnvironment("Mailserver__Hostname", mailserverHostname)
        .WithEnvironment("Mailserver__Port", mailserverPort)
        .WithEnvironment("Mailserver__FromAddress", mailserverFromAddress);
}

builder.Build().Run();
