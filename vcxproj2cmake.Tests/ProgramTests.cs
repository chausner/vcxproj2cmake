using Xunit;

namespace vcxproj2cmake.Tests;

public class ProgramTests
{
    [Fact]
    public void When_InvokedWithArgsHelp_Then_PrintsHelpAndReturnsZero()
    {
        // Act
        var (stdout, stderr, exitCode) = RunProgramMainWithCapturedConsole("--help");

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("Convert Microsoft Visual C++ projects and solutions to CMake", stdout);
        Assert.True(string.IsNullOrEmpty(stderr));
    }

    [Fact]
    public void When_InvokedWithArgsVersion_Then_PrintsVersionAndReturnsZero()
    {
        const string SemVerRegex = @"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-((?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+([0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?\s*$";

        // Act
        var (stdout, stderr, exitCode) = RunProgramMainWithCapturedConsole("--version");

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Matches(SemVerRegex, stdout);
        Assert.True(string.IsNullOrEmpty(stderr));
    }

    [Fact]
    public void When_InvokedWithArgsProjectsAndAllOptions_Then_ReturnsZero()
    {
        var repoRoot = FindRepoRoot();
        var appProj = Path.Combine(repoRoot, "ExampleSolution", "App", "App.vcxproj");
        var mathLibProj = Path.Combine(repoRoot, "ExampleSolution", "MathLib", "MathLib.vcxproj");

        // Act
        var (_, _, exitCode) = RunProgramMainWithCapturedConsole(
            "--projects", appProj, mathLibProj,
            "--qt-version", "6",
            "--portable",
            "--include-headers",
            "--enable-standalone-project-builds",
            "--indent-style", "Tabs",
            "--indent-size", "2",
            "--dry-run",
            "--log-level", "Debug");

        // Assert
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void When_InvokedWithArgsSolutionAndAllOptions_Then_ReturnsZero()
    {
        var repoRoot = FindRepoRoot();
        var sln = Path.Combine(repoRoot, "ExampleSolution", "ExampleSolution.sln");

        // Act
        var (_, _, exitCode) = RunProgramMainWithCapturedConsole(
            "--solution", sln,
            "--qt-version", "5",
            "--include-headers",
            "--enable-standalone-project-builds",
            "--indent-style", "Spaces",
            "--indent-size", "4",
            "--dry-run",
            "--log-level", "Information");

        // Assert
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void When_InvokedWithoutArgsProjectsAndSolution_Then_ReturnsNonZeroAndPrintsError()
    {
        // Act
        var (_, stderr, exitCode) = RunProgramMainWithCapturedConsole(
            "--dry-run");

        // Assert
        Assert.NotEqual(0, exitCode);
        Assert.Contains("Either --projects or --solution must be specified.", stderr);
    }

    [Fact]
    public void When_InvokedWithArgsProjectsAndSolution_Then_ReturnsNonZeroAndPrintsError()
    {
        var repoRoot = FindRepoRoot();
        var sln = Path.Combine(repoRoot, "ExampleSolution", "ExampleSolution.sln");
        var appProj = Path.Combine(repoRoot, "ExampleSolution", "App", "App.vcxproj");

        // Act
        var (_, stderr, exitCode) = RunProgramMainWithCapturedConsole(
            "--projects", appProj,
            "--solution", sln);

        // Assert
        Assert.NotEqual(0, exitCode);
        Assert.Contains("Only one of --projects or --solution can be specified.", stderr);
    }

    [Fact]
    public void When_UnexpectedExceptionOccurs_Then_ReturnsNonZeroAndPrintsError()
    {
        // Arrange
        var repoRoot = FindRepoRoot();
        var sln = Path.Combine(repoRoot, "ExampleSolution", "ExampleSolution.sln");

        // Open the solution file with an exclusive lock to simulate an IOException when trying to read it
        using var fileLock = File.Open(sln, FileMode.Open, FileAccess.Read, FileShare.None);

        // Act
        var (stdout, stderr, exitCode) = RunProgramMainWithCapturedConsole(
            "--solution", sln);

        // Assert
        Assert.NotEqual(0, exitCode);
        Assert.Contains("Unexpected error", stdout);
        Assert.Contains("System.IO.IOException: The process cannot access the file", stdout);
    }

    static (string stdout, string stderr, int exitCode) RunProgramMainWithCapturedConsole(params string[] args)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;

        try
        {
            using var outWriter = new StringWriter();
            using var errorWriter = new StringWriter();

            Console.SetOut(outWriter);
            Console.SetError(errorWriter);

            int exitCode = Program.Main(args);

            outWriter.Flush();
            errorWriter.Flush();

            return (outWriter.ToString(), errorWriter.ToString(), exitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;

        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "vcxproj2cmake.slnx")))
                return dir;

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new InvalidOperationException("Could not locate repository root");
    }
}
