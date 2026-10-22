using NSwag.Generation;

namespace AppTemplate.Web.Configurations;

/// <summary>
/// Writes the OpenAPI document to a file and exits, for the committed contract
/// (<c>backend/openapi/v1.json</c>) that the Angular types are generated from:
/// <code>dotnet run --project src/AppTemplate.Web --no-launch-profile -- --OpenApi:ExportPath=../../openapi/v1.json</code>
/// The path is relative to the Web project. Runs right after the endpoints are mapped and before
/// anything touches Postgres, Redis or Keycloak, so it needs no running services.
/// </summary>
public static class OpenApiExport
{
    public const string ExportPathKey = "OpenApi:ExportPath";
    public const string DocumentName = "v1";

    /// <returns><c>true</c> when the document was exported and the app should stop.</returns>
    public static async Task<bool> TryExportOpenApiAsync(this WebApplication app)
    {
        var exportPath = app.Configuration[ExportPathKey];
        if (string.IsNullOrEmpty(exportPath))
        {
            return false;
        }

        // WebApplication only hands its endpoints to routing (where the OpenAPI generator looks for
        // them) when the host starts. Nothing above this call in Program.cs touches a database.
        await app.StartAsync();
        NSwag.OpenApiDocument document;
        try
        {
            document = await app
                .Services.GetRequiredService<IOpenApiDocumentGenerator>()
                .GenerateAsync(DocumentName);
        }
        finally
        {
            await app.StopAsync();
        }

        var fullPath = Path.GetFullPath(exportPath, app.Environment.ContentRootPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        // LF on every OS, so the committed file doesn't change with the machine that wrote it.
        await File.WriteAllTextAsync(fullPath, document.ToJson().ReplaceLineEndings("\n") + "\n");

        app.Logger.LogInformation(
            "Exported OpenAPI document {Document} to {Path}",
            DocumentName,
            fullPath
        );
        return true;
    }
}
