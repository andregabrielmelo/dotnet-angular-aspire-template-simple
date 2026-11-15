#!/usr/bin/env dotnet
// Runs what CI runs (backend-build.yml and frontend-build.yml), locally, and prints a summary.
// A .NET 10 file-based app, after NimblePros/eShopOnWeb's BuildTestFormat.cs. From the repository root:
//
//   dotnet scripts/BuildTestFormat.cs               everything
//   dotnet scripts/BuildTestFormat.cs --backend     only the backend
//   dotnet scripts/BuildTestFormat.cs --frontend    only the frontend
//   dotnet scripts/BuildTestFormat.cs --no-docker   skip the tests that need Docker (Category!=RequiresDocker)
//
// Docker-backed tests use your environment as they are (on Podman: DOCKER_HOST and
// TESTCONTAINERS_RYUK_DISABLED=true, see CLAUDE.md). A stale backend/openapi/v1.json or
// frontend/src/app/core/api/api-types.ts is regenerated and reported: review it and commit it.
// Output of a step is shown only when it fails.

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

var arguments = args.ToHashSet(StringComparer.Ordinal);
var unknown = arguments.Except(["--backend", "--frontend", "--no-docker"]).ToList();
if (unknown.Count > 0)
{
    Console.Error.WriteLine($"Unknown option(s): {string.Join(' ', unknown)}");
    Console.Error.WriteLine(
        "Usage: dotnet scripts/BuildTestFormat.cs [--backend | --frontend] [--no-docker]"
    );
    return 2;
}

var runBackend = !arguments.Contains("--frontend") || arguments.Contains("--backend");
var runFrontend = !arguments.Contains("--backend") || arguments.Contains("--frontend");
var noDocker = arguments.Contains("--no-docker");

var root = FindRepositoryRoot();
var backend = Path.Combine(root, "backend");
var frontend = Path.Combine(root, "frontend");
var npm = OperatingSystem.IsWindows() ? "npm.cmd" : "npm";
var results = new List<StepResult>();
var overall = Stopwatch.StartNew();

if (runBackend)
{
    var built =
        await Step(
            "backend",
            "restore (locked)",
            "dotnet",
            "restore AppTemplate.slnx --locked-mode",
            backend
        )
        && await Step("backend", "format (csharpier)", "dotnet", "tool restore", backend)
        && await Step("backend", "format (csharpier)", "dotnet", "csharpier check .", backend)
        && await Step(
            "backend",
            "build (Release)",
            "dotnet",
            "build AppTemplate.slnx --configuration Release --no-restore",
            backend
        );

    if (built)
    {
        var filter = noDocker ? " --filter \"Category!=RequiresDocker\"" : "";
        foreach (
            var project in Directory
                .GetFiles(Path.Combine(backend, "tests"), "*.csproj", SearchOption.AllDirectories)
                .Order(StringComparer.Ordinal)
        )
        {
            await TestStep(
                project,
                $"test --no-build --configuration Release{filter} \"{project}\""
            );
        }

        await StaleFileStep(
            "backend",
            "OpenAPI document current",
            Path.Combine(backend, "openapi", "v1.json"),
            "bash",
            "scripts/export-openapi.sh --no-build",
            root
        );
    }
    else
    {
        Skip("backend", "tests and OpenAPI check", "the restore, format tool or build failed");
    }
}

if (runFrontend)
{
    // npm writes node_modules/.package-lock.json at the end of every install, so its absence means
    // no install, or an unfinished one (or one made by another OS's npm).
    var installed = File.Exists(Path.Combine(frontend, "node_modules", ".package-lock.json"))
        ? Record(
            "frontend",
            "install",
            true,
            TimeSpan.Zero,
            "node_modules is installed (run npm ci yourself after a package change)"
        ).Passed
        : await Step("frontend", "install (npm ci)", npm, "ci", frontend);

    if (installed)
    {
        await StaleFileStep(
            "frontend",
            "API types current",
            Path.Combine(frontend, "src", "app", "core", "api", "api-types.ts"),
            npm,
            "run api:generate",
            frontend
        );
        await Step("frontend", "format (prettier)", npm, "run format:check", frontend);
        await Step("frontend", "lint (eslint)", npm, "run lint", frontend);
        await Step("frontend", "test (vitest)", npm, "run test", frontend);
        await Step("frontend", "build", npm, "run build", frontend);
    }
    else
    {
        Skip("frontend", "checks", "npm ci failed");
    }
}

PrintSummary();
return results.All(result => result.Passed) ? 0 : 1;

async Task<bool> Step(
    string section,
    string name,
    string fileName,
    string arguments,
    string workingDirectory
)
{
    Console.Write($"[{section}] {name}... ");
    var run = await RunAsync(fileName, arguments, workingDirectory);
    var passed = run.ExitCode == 0;
    Console.WriteLine(passed ? $"ok ({Format(run.Elapsed)})" : $"FAILED ({Format(run.Elapsed)})");
    if (!passed)
    {
        WriteFailure(run);
    }
    // A step split in two commands (tool restore, then check) is one row in the summary.
    var existing = results.FindIndex(result => result.Section == section && result.Name == name);
    if (existing >= 0)
    {
        var previous = results[existing];
        results[existing] = previous with
        {
            Passed = previous.Passed && passed,
            Elapsed = previous.Elapsed + run.Elapsed,
        };
    }
    else
    {
        results.Add(new StepResult(section, name, passed, run.Elapsed, null));
    }
    return passed;
}

async Task TestStep(string project, string arguments)
{
    var name = $"test {Path.GetFileNameWithoutExtension(project)}";
    Console.Write($"[backend] {name}... ");
    var run = await RunAsync("dotnet", arguments, backend);
    var counts = TestCounts(run.Output);
    var passed = run.ExitCode == 0;
    Console.WriteLine(
        $"{(passed ? "ok" : "FAILED")} ({Format(run.Elapsed)}{(counts is null ? "" : $", {counts}")})"
    );
    if (!passed)
    {
        WriteFailure(run);
    }
    results.Add(new StepResult("backend", name, passed, run.Elapsed, counts));
}

// Regenerates a committed, generated file and fails if that changed it, as CI does. The file is
// compared with what was on disk before, so uncommitted but current changes don't count as stale.
async Task StaleFileStep(
    string section,
    string name,
    string file,
    string fileName,
    string arguments,
    string workingDirectory
)
{
    Console.Write($"[{section}] {name}... ");
    var before = Hash(file);
    var run = await RunAsync(fileName, arguments, workingDirectory);
    if (run.ExitCode != 0)
    {
        Console.WriteLine($"FAILED ({Format(run.Elapsed)})");
        WriteFailure(run);
        results.Add(new StepResult(section, name, false, run.Elapsed, "could not regenerate"));
        return;
    }
    var current = before == Hash(file);
    var relative = Path.GetRelativePath(root, file);
    Console.WriteLine(current ? $"ok ({Format(run.Elapsed)})" : $"STALE ({Format(run.Elapsed)})");
    if (!current)
    {
        Console.WriteLine(
            $"    {relative} was stale and has been regenerated: review it and commit it."
        );
    }
    results.Add(
        new StepResult(
            section,
            name,
            current,
            run.Elapsed,
            current ? null : $"{relative} regenerated: review and commit"
        )
    );
}

StepResult Record(string section, string name, bool passed, TimeSpan elapsed, string? note)
{
    var result = new StepResult(section, name, passed, elapsed, note);
    results.Add(result);
    Console.WriteLine($"[{section}] {name}... {(passed ? "ok" : "FAILED")} ({note})");
    return result;
}

void Skip(string section, string name, string reason)
{
    results.Add(new StepResult(section, name, false, TimeSpan.Zero, $"skipped: {reason}"));
    Console.WriteLine($"[{section}] {name}... skipped ({reason})");
}

void PrintSummary()
{
    Console.WriteLine();
    Console.WriteLine("Summary");
    var width = results.Max(result => $"{result.Section} {result.Name}".Length);
    foreach (var result in results)
    {
        var label = $"{result.Section} {result.Name}".PadRight(width);
        var note = result.Note is null ? "" : $"  {result.Note}";
        Console.WriteLine(
            $"  {(result.Passed ? "pass" : "FAIL")}  {label}  {Format(result.Elapsed), 8}{note}"
        );
    }
    var failed = results.Count(result => !result.Passed);
    Console.WriteLine();
    Console.WriteLine(
        failed == 0
            ? $"All {results.Count} checks passed in {Format(overall.Elapsed)}."
            : $"{failed} of {results.Count} checks failed ({Format(overall.Elapsed)})."
    );
}

static async Task<RunResult> RunAsync(string fileName, string arguments, string workingDirectory)
{
    var startInfo = new ProcessStartInfo(fileName, arguments)
    {
        WorkingDirectory = workingDirectory,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
    };
    var output = new StringBuilder();
    var stopwatch = Stopwatch.StartNew();
    using var process = new Process { StartInfo = startInfo };
    process.OutputDataReceived += (_, line) => Append(line.Data);
    process.ErrorDataReceived += (_, line) => Append(line.Data);
    try
    {
        process.Start();
    }
    catch (System.ComponentModel.Win32Exception exception)
    {
        return new RunResult(
            -1,
            $"Could not start {fileName}: {exception.Message}",
            stopwatch.Elapsed
        );
    }
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();
    await process.WaitForExitAsync();
    return new RunResult(process.ExitCode, output.ToString(), stopwatch.Elapsed);

    void Append(string? line)
    {
        if (line is null)
        {
            return;
        }
        lock (output)
        {
            output.AppendLine(line);
        }
    }
}

static void WriteFailure(RunResult run)
{
    // The tail is where the errors are; the whole output can be thousands of lines.
    var lines = run.Output.Split('\n');
    foreach (var line in lines.TakeLast(60))
    {
        Console.WriteLine($"    {line.TrimEnd('\r')}");
    }
}

// "Passed!  - Failed:     0, Passed:    62, Skipped:     0, Total:    62" (the VSTest summary).
static string? TestCounts(string output)
{
    var match = Regex.Match(
        output,
        @"Failed:\s+(\d+), Passed:\s+(\d+), Skipped:\s+(\d+), Total:\s+(\d+)"
    );
    if (!match.Success)
    {
        return null;
    }
    return $"{match.Groups[2].Value}/{match.Groups[4].Value} passed"
        + (match.Groups[1].Value == "0" ? "" : $", {match.Groups[1].Value} failed")
        + (match.Groups[3].Value == "0" ? "" : $", {match.Groups[3].Value} skipped");
}

static string? Hash(string file) =>
    File.Exists(file) ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))) : null;

static string Format(TimeSpan elapsed) =>
    elapsed.TotalMinutes >= 1
        ? $"{(int)elapsed.TotalMinutes}m {elapsed.Seconds:00}s"
        : $"{elapsed.TotalSeconds:0.0}s";

static string FindRepositoryRoot()
{
    for (
        var directory = new DirectoryInfo(Environment.CurrentDirectory);
        directory is not null;
        directory = directory.Parent
    )
    {
        if (File.Exists(Path.Combine(directory.FullName, "backend", "AppTemplate.slnx")))
        {
            return directory.FullName;
        }
    }
    throw new InvalidOperationException(
        "Run this from inside the repository (no backend/AppTemplate.slnx above the current directory)."
    );
}

record RunResult(int ExitCode, string Output, TimeSpan Elapsed);

record StepResult(string Section, string Name, bool Passed, TimeSpan Elapsed, string? Note);
