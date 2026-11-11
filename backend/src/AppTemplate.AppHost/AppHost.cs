using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// Add Postgre SQL Server container
var postgres = builder
    .AddPostgres("postgres")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

// Add the database
var applicationDatabase = postgres.AddDatabase("apptemplate");

// register the API project and link the DB
var api = builder
    .AddProject<Projects.AppTemplate_Web>("web")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName)
    .WithReference(applicationDatabase)
    .WaitFor(applicationDatabase)
    // Readiness (Postgres reachable): resources that WaitFor(api) start only once it's ready.
    .WithHttpHealthCheck("/health");

// The Angular dev server is the browser's single origin: its proxy (frontend/proxy.conf.js)
// forwards /api to the Web API, found through the services__web__http__0 variable that
// WithReference(api) sets. Same origin, so there is no CORS.
var frontend = builder
    .AddJavaScriptApp("angular", "../../../frontend", runScriptName: "start")
    .WithNpm(installCommand: "ci")
    .WithHttpEndpoint(env: "PORT")
    .WithHttpHealthCheck("/")
    .WithReference(api)
    .WaitFor(api)
    .WithExternalHttpEndpoints()
    .PublishAsDockerFile();

// Account emails (confirmation, password reset) link to the Angular app's pages.
api.WithEnvironment("AccountLinks__ClientUrl", frontend.GetEndpoint("http"));

if (builder.Environment.IsDevelopment())
{
    // Development SMTP catcher: the API's emails (IEmailSender, such as the welcome email) go
    // here, and can be read in Mailpit's web UI (the "mailpit" resource's http endpoint).
    var mailpit = builder.AddMailPit("mailpit", httpPort: 8025, smtpPort: 1025);

    var smtp = mailpit.GetEndpoint("smtp");
    api.WithEnvironment("Mailserver__Hostname", smtp.Property(EndpointProperty.Host))
        .WithEnvironment("Mailserver__Port", smtp.Property(EndpointProperty.Port))
        .WithEnvironment("Mailserver__FromAddress", "noreply@apptemplate.local")
        .WaitFor(mailpit);

    // The Angular dev server's proxy reaches the API from loopback: trust its X-Forwarded-For,
    // so rate limits count the browser's address (ADR 014).
    api.WithEnvironment("ForwardedHeaders__KnownProxies__0", "127.0.0.1")
        .WithEnvironment("ForwardedHeaders__KnownProxies__1", "::1");
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
