using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// Add Postgre SQL Server container
var postgres = builder
    .AddPostgres("postgres")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

// Add the database
var applicationDatabase = postgres.AddDatabase("apptemplate");

// register the API project and link the DB. HTTPS only (its "https" launch profile), with the
// ASP.NET Core development certificate (ADR 016).
var api = builder
    .AddProject<Projects.AppTemplate_Web>("web", launchProfileName: "https")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName)
    .WithReference(applicationDatabase)
    .WaitFor(applicationDatabase)
    // Readiness (Postgres reachable): resources that WaitFor(api) start only once it's ready.
    .WithHttpHealthCheck("/health", endpointName: "https");

// The API's settings kept in the AppHost's appsettings ("web" section), passed on as environment
// variables: Mailserver:FromAddress becomes Mailserver__FromAddress, and an array element such as
// ForwardedHeaders:KnownProxies:0 becomes ForwardedHeaders__KnownProxies__0. In development they
// are Mailpit's sender and loopback as a known proxy: the Angular dev server's proxy reaches the
// API from loopback, so trusting its X-Forwarded-For makes rate limits count the browser's
// address (ADR 014).
foreach (
    var (key, value) in builder
        .Configuration.GetSection("web")
        .AsEnumerable(makePathsRelative: true)
)
{
    if (value is not null)
    {
        api.WithEnvironment(key.Replace(":", "__"), value);
    }
}

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
    // The SMTP server the API's emails (IEmailSender) go to.
    var mailserverHostname = builder.AddParameter("mailserver-hostname");
    var mailserverPort = builder.AddParameter("mailserver-port");
    var mailserverFromAddress = builder.AddParameter("mailserver-from-address");

    api.WithEnvironment("Mailserver__Hostname", mailserverHostname)
        .WithEnvironment("Mailserver__Port", mailserverPort)
        .WithEnvironment("Mailserver__FromAddress", mailserverFromAddress);
}

builder.Build().Run();
