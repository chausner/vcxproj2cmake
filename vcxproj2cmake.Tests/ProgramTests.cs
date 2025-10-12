using Xunit;

namespace vcxproj2cmake.Tests;

public class ProgramTests
{
    [Fact]
    public void When_InvokedWithArgsHelp_Then_PrintsHelpAndReturnsZero()
    {
        var (stdout, stderr, exitCode) = RunProgramMainWithCapturedConsole("--help");

        Assert.Equal(0, exitCode);
        Assert.Contains("Converts Microsoft Visual C++ projects and solutions to CMake", stdout);
        Assert.True(string.IsNullOrEmpty(stderr));
    }

    [Fact]
    public void When_InvokedWithArgsProjectsAndAllOptions_Then_ReturnsZero()
    {
        var repoRoot = FindRepoRoot();
        var appProj = Path.Combine(repoRoot, "ExampleSolution", "App", "App.vcxproj");
        var mathLibProj = Path.Combine(repoRoot, "ExampleSolution", "MathLib", "MathLib.vcxproj");

        var (_, _, exitCode) = RunProgramMainWithCapturedConsole(
            "--projects", appProj, mathLibProj,
            "--qt-version", "6",
            "--enable-standalone-project-builds",
            "--indent-style", "Tabs",
            "--indent-size", "2",
            "--dry-run",
            "--log-level", "Debug");

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void When_InvokedWithArgsSolutionAndAllOptions_Then_ReturnsZero()
    {
        var repoRoot = FindRepoRoot();
        var sln = Path.Combine(repoRoot, "ExampleSolution", "ExampleSolution.sln");

        var (_, _, exitCode) = RunProgramMainWithCapturedConsole(
            "--solution", sln,
            "--qt-version", "5",
            "--enable-standalone-project-builds",
            "--indent-style", "Spaces",
            "--indent-size", "4",
            "--dry-run",
            "--log-level", "Information");

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void When_InvokedWithArgsProjectsAndSolution_Then_ReturnsNonZeroAndPrintsError()
    {
        var repoRoot = FindRepoRoot();
        var sln = Path.Combine(repoRoot, "ExampleSolution", "ExampleSolution.sln");
        var appProj = Path.Combine(repoRoot, "ExampleSolution", "App", "App.vcxproj");

        var (_, stderr, exitCode) = RunProgramMainWithCapturedConsole(
            "--projects", appProj,
            "--solution", sln);

        Assert.NotEqual(0, exitCode);
        Assert.Contains("Specify either --projects or --solution, but not both.", stderr);
    }

    private static (string stdout, string stderr, int exitCode) RunProgramMainWithCapturedConsole(params string[] args)
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

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;

        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "vcxproj2cmake.sln")))
                return dir;

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new InvalidOperationException("Could not locate repository root");
    }
}
