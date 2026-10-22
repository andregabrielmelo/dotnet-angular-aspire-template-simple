var builder = DistributedApplication.CreateBuilder(args);

// Add Postgre SQL Server container
var postgres = builder
    .AddPostgres("postgres")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

// Add the database
var applicationDatabase = postgres.AddDatabase("apptemplate");

// OpenID Connect provider: hosts the login, registration and logout pages. The realm (clients,
// audience mapper, registration enabled) is imported from Realms/ on first start. The port is
// pinned so the issuer URL the browser sees stays stable across runs.
var backendForFrontendSecret = builder.AddParameter(
    "keycloak-backend-for-frontend-secret",
    secret: true
);

// Service account the Web API uses for Keycloak's Admin API (password reset emails).
var userAdminSecret = builder.AddParameter("keycloak-user-admin-secret", secret: true);

// Development SMTP catcher: Keycloak sends its emails (e.g. password reset links) here, and
// they can be read in Mailpit's web UI (the "mailpit" resource's http endpoint). The registry is
// explicit because Podman, unlike Docker, does not assume docker.io for unqualified image names.
var mailpit = builder
    .AddContainer("mailpit", "axllent/mailpit", "v1.31.2")
    .WithImageRegistry("docker.io")
    .WithHttpEndpoint(targetPort: 8025, name: "http")
    .WithEndpoint(targetPort: 1025, name: "smtp", scheme: "tcp");

var keycloak = builder
    .AddKeycloak("keycloak", port: 8080)
    .WithDataVolume()
    .WithRealmImport("./Realms")
    .WithLifetime(ContainerLifetime.Persistent)
    .WaitFor(mailpit);

// Redis: HybridCache's distributed (L2) cache and the output cache store, shared by every
// API instance so cache invalidations reach all of them.
var cache = builder.AddRedis("cache");

// register the API project and link the DB
var api = builder
    .AddProject<Projects.AppTemplate_Web>("web")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName)
    .WithReference(applicationDatabase)
    .WaitFor(applicationDatabase)
    .WithReference(keycloak)
    .WaitFor(keycloak)
    .WithReference(cache)
    .WaitFor(cache)
    .WithEnvironment("Keycloak__Admin__ClientSecret", userAdminSecret)
    // Background jobs (welcome emails) send through Mailpit too.
    .WithEnvironment(
        "Mailserver__Hostname",
        mailpit.GetEndpoint("smtp").Property(EndpointProperty.Host)
    )
    .WithEnvironment(
        "Mailserver__Port",
        mailpit.GetEndpoint("smtp").Property(EndpointProperty.Port)
    )
    .WaitFor(mailpit)
    .WithHttpEndpoint(name: "api-http")
    // Readiness (Postgres reachable): resources that WaitFor(api) start only once it's ready.
    .WithHttpHealthCheck("/health")
    // Hangfire dashboard (Development only, local requests only).
    .WithUrlForEndpoint(
        "http",
        _ => new ResourceUrlAnnotation { Url = "/hangfire", DisplayText = "Jobs dashboard" }
    );

// Angular dev server - only reached through the backend for frontend, never directly.
var frontend = builder
    .AddJavaScriptApp("angular", "../../../frontend", runScriptName: "start")
    .WithNpm(installCommand: "ci")
    .WithHttpEndpoint(env: "PORT")
    .WithHttpHealthCheck("/")
    .PublishAsDockerFile();

// The browser's single entry point: owns the session cookie, runs the OIDC flow against
// Keycloak, and proxies /api to the Web API (adding the access token) and everything else to
// the Angular dev server. Its port is pinned because the realm's redirect URIs point at it.
var backendForFrontend = builder
    .AddProject<Projects.AppTemplate_BackendForFrontend>(
        "backend-for-frontend",
        launchProfileName: null
    )
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName)
    .WithHttpsEndpoint(port: 7100, name: "https")
    .WithHttpHealthCheck("/health", endpointName: "https")
    .WithEnvironment("Keycloak__ClientSecret", backendForFrontendSecret)
    .WithReference(keycloak)
    .WaitFor(keycloak)
    .WithReference(api)
    .WaitFor(api)
    .WithReference(frontend.GetEndpoint("http"))
    .WaitFor(frontend)
    .WithExternalHttpEndpoints();

ExternalIdentityProviders.Configure(builder, keycloak, backendForFrontend);

builder.Build().Run();
