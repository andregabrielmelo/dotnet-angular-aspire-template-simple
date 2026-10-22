using AppTemplate.BackendForFrontend.Configurations;
using AppTemplate.ServiceDefaults;
using AppTemplate.ServiceDefaults.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddAuthenticationConfigurations(builder);
builder.Services.AddReverseProxyConfigurations(builder);

// This host's own error responses (CSRF and session 401s, 403s, failures) use the same RFC 9457
// problem details contract as the Web API. Proxied API responses pass through untouched.
builder.Services.AddProblemDetails();

var app = builder.Build();

// First, so its OnStarting callback covers every response: errors, proxied ones, the SPA.
app.UseSecurityHeaders();
app.UseDefaultRequestLogging();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
    app.UseHsts();
}

// Gives empty 4xx/5xx responses a problem details body; a response that already has a body
// (anything proxied from the API or the SPA) is left alone.
app.UseStatusCodePages();

app.UseHttpsRedirection();

// Outside Development the built Angular app is served from wwwroot; in Development the
// "spa" YARP route forwards everything else to the Angular dev server instead.
if (!app.Environment.IsDevelopment())
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.UseAntiforgeryHeaderCheck();

app.UseAuthentication();
app.UseAuthorization();

app.MapBackendForFrontendEndpoints();
app.MapReverseProxyWithAccessTokens();

if (!app.Environment.IsDevelopment())
{
    app.MapFallbackToFile("index.html");
}

app.MapDefaultEndpoints();

app.Run();

// Make the implicit Program class public so tests can host this app with WebApplicationFactory.
public partial class Program { }
