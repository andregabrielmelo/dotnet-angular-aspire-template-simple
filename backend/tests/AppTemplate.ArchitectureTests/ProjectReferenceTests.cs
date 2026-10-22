using System.Xml.Linq;
using Xunit;

namespace AppTemplate.ArchitectureTests;

/// <summary>
/// Reads the <c>ProjectReference</c>s straight from each <c>src/*.csproj</c>. The compiled
/// assemblies only show references the code actually uses, so an unused but forbidden
/// reference (the first step of a layering violation) is only visible here.
/// </summary>
public class ProjectReferenceTests
{
    /// <summary>Which projects each project may reference. Dependencies point inward (ADR 001).</summary>
    private static readonly Dictionary<string, string[]> AllowedReferences = new()
    {
        ["AppTemplate.SharedKernel"] = [],
        ["AppTemplate.Core"] = ["AppTemplate.SharedKernel"],
        ["AppTemplate.UseCases"] = ["AppTemplate.Core", "AppTemplate.SharedKernel"],
        ["AppTemplate.Infrastructure"] =
        [
            "AppTemplate.Core",
            "AppTemplate.UseCases",
            "AppTemplate.SharedKernel",
        ],
        // The composition root wires everything together.
        ["AppTemplate.Web"] =
        [
            "AppTemplate.Core",
            "AppTemplate.UseCases",
            "AppTemplate.Infrastructure",
            "AppTemplate.SharedKernel",
            "AppTemplate.ServiceDefaults",
        ],
        ["AppTemplate.ServiceDefaults"] = [],
        // Sits outside the layers and never sees the domain (ADR 007).
        ["AppTemplate.BackendForFrontend"] = ["AppTemplate.ServiceDefaults"],
        // Aspire project resources, for local orchestration only (ADR 002).
        ["AppTemplate.AppHost"] = ["AppTemplate.Web", "AppTemplate.BackendForFrontend"],
    };

    public static TheoryData<string> SourceProjects()
    {
        var data = new TheoryData<string>();
        foreach (var project in FindSourceProjects().Keys.Order())
        {
            data.Add(project);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(SourceProjects))]
    public void Project_ReferencesOnlyAllowedProjects(string project)
    {
        Assert.True(
            AllowedReferences.TryGetValue(project, out var allowed),
            $"{project} is new: add it to {nameof(AllowedReferences)} with the projects it may reference."
        );

        var forbidden = ProjectReferencesOf(FindSourceProjects()[project]).Except(allowed).ToList();

        Assert.True(
            forbidden.Count == 0,
            $"{project} must not reference {string.Join(", ", forbidden)} (see ADR 001)."
        );
    }

    private static Dictionary<string, string> FindSourceProjects() =>
        Directory
            .GetFiles(
                Path.Combine(FindBackendDirectory(), "src"),
                "*.csproj",
                SearchOption.AllDirectories
            )
            .ToDictionary(path => Path.GetFileNameWithoutExtension(path)!, path => path);

    private static IEnumerable<string> ProjectReferencesOf(string csprojPath) =>
        XDocument
            .Load(csprojPath)
            .Descendants("ProjectReference")
            .Select(reference => reference.Attribute("Include")!.Value.Replace('\\', '/'))
            .Select(Path.GetFileNameWithoutExtension)
            .OfType<string>();

    private static string FindBackendDirectory()
    {
        for (
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            directory is not null;
            directory = directory.Parent
        )
        {
            if (directory.GetFiles("*.slnx").Length > 0)
            {
                return directory.FullName;
            }
        }
        throw new InvalidOperationException(
            $"No .slnx file above {AppContext.BaseDirectory}; run the tests from the repository."
        );
    }
}
