using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

static class AssertExtensions
{
    extension(Assert)
    {
        public static void FileHasContent(string path, MockFileSystem fileSystem, string content)
        {
            var trimmedExpectedContent = content.Trim();
            var trimmedContent = fileSystem.GetFile(path).TextContents.Trim();
            Assert.Equal(trimmedExpectedContent, trimmedContent);
        }
    }
}

static class MockFileSystemExtensions
{
    extension(MockFileSystem fileSystem)
    {
        public void CopyCurrentDirectoryToDisk(string destinationDirectory)
        {
            var currentDirectory = fileSystem.Directory.GetCurrentDirectory();
            var currentDirectoryInfo = fileSystem.DirectoryInfo.New(currentDirectory);

            foreach (var directory in currentDirectoryInfo.EnumerateDirectories("*", SearchOption.AllDirectories))
            {
                var relativePath = fileSystem.Path.GetRelativePath(currentDirectory, directory.FullName);
                var destinationPath = Path.Combine(destinationDirectory, relativePath);

                Directory.CreateDirectory(destinationPath);
            }

            foreach (var file in currentDirectoryInfo.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                var relativePath = fileSystem.Path.GetRelativePath(currentDirectory, file.FullName);
                var destinationPath = Path.Combine(destinationDirectory, relativePath);

                using var sourceStream = file.OpenRead();
                using var destinationStream = File.Create(destinationPath);
                sourceStream.CopyTo(destinationStream);
            }
        }
    }
}

internal class InMemoryLogger : ILogger
{
    public ConcurrentQueue<string> Messages { get; } = new();
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public string AllMessageText => string.Join(Environment.NewLine, Messages);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Messages.Enqueue(formatter(state, exception));
    }
}

static class CMakeAssert
{
    public static async Task<(ProcessOutput ConfigureOutput, ProcessOutput BuildOutput)> ConfiguresAndBuildsWithCMake(
        MockFileSystem fileSystem, 
        string? architecture = null, 
        string? configuration = null)
    {
        if (!await CanRunCMake())
            Assert.Skip("CMake is not available on PATH.");

        var sourceDir = Path.Combine(Path.GetTempPath(), $"vcxproj2cmake.Tests-{Guid.NewGuid():N}");
        var buildDir = Path.Combine(sourceDir, "build");

        try
        {
            Directory.CreateDirectory(sourceDir);
            fileSystem.CopyCurrentDirectoryToDisk(sourceDir);

            var configureArgs = new List<string> { "-S", sourceDir, "-B", buildDir };
            if (architecture != null)
                configureArgs.AddRange(["-A", architecture]);

            var configureOutput = await RunCMake(configureArgs, sourceDir);

            var buildArgs = new List<string> { "--build", buildDir };
            if (configuration != null)
                buildArgs.AddRange(["--config", configuration]);

            var buildOutput = await RunCMake(buildArgs, sourceDir);

            return (configureOutput, buildOutput);
        }
        finally
        {
            Directory.Delete(sourceDir, recursive: true);
        }
    }

    static readonly Lazy<Task<bool>> canRunCMake = new(async () =>
    {
        try
        {
            await RunCMake(["--version"], Environment.CurrentDirectory);
            return true;
        }
        catch
        {
            return false;
        }
    });

    static Task<bool> CanRunCMake() => canRunCMake.Value;

    static async Task<ProcessOutput> RunCMake(IEnumerable<string> arguments, string workingDirectory)
    {
        using var process = new Process();
        process.StartInfo.FileName = "cmake";
        process.StartInfo.WorkingDirectory = workingDirectory;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        foreach (var argument in arguments)
            process.StartInfo.ArgumentList.Add(argument);

        process.Start();
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        await Task.WhenAll(outputTask, errorTask, process.WaitForExitAsync());
        string output = await outputTask;
        string error = await errorTask;

        Assert.True(process.ExitCode == 0, $"""
            cmake {string.Join(' ', arguments)} failed with exit code {process.ExitCode}:
            {output}
            {error}
            """);

        return new ProcessOutput(output, error);
    }
}

record ProcessOutput(string Stdout, string Stderr);