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

// Refresh and sign-out only accept requests whose Origin is the app's (AuthCookies).
api.WithEnvironment("Authentication__AllowedOrigins__0", frontend.GetEndpoint("http"));

// The access token signing key: base64 of at least 32 random bytes. In Development it is
// generated once (64 letters and digits are valid base64 for 48 bytes) and kept in this
// project's user secrets; elsewhere supply it, e.g. from `openssl rand -base64 64`.
var jwtSigningKey = builder.Environment.IsDevelopment()
    ? builder.AddParameter(
        "jwt-signing-key",
        new GenerateParameterDefault { MinLength = 64, Special = false },
        secret: true,
        persist: true
    )
    : builder.AddParameter("jwt-signing-key", secret: true);
api.WithEnvironment("Jwt__SigningKey", jwtSigningKey);

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

    api.WithEnvironment("Mailserver__Hostname", mailserverHostname)
        .WithEnvironment("Mailserver__Port", mailserverPort);
}

builder.Build().Run();
