using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;

namespace vcxproj2cmake;

static class Program
{
    static ILogger? logger;

    static int Main(string[] args)
    {
        var projectsOption = new Option<List<string>?>(
            name: "--projects",
            description: "Paths to one or multiple .vcxproj files")
        {
            AllowMultipleArgumentsPerToken = true,
            ArgumentHelpName = "path(s)"
        };

        var solutionOption = new Option<string?>(
            name: "--solution",
            description: "Path to a solution .sln file")
        { 
            ArgumentHelpName = "path" 
        };

        var qtVersionOption = new Option<int?>(
            name: "--qt-version",
            description: "Set Qt version (required for Qt projects)")
            .FromAmong("5", "6");

        var enableStandaloneProjectBuildsOption = new Option<bool>(
            name: "--enable-standalone-project-builds",
            description: "Generate necessary code to allow projects to be built standalone (not through the root CMakeLists.txt)");

        var dryRunOption = new Option<bool>(
            name: "--dry-run",
            description: "Print generated output to the console, do not store generated files");

        var logLevelOption = new Option<LogLevel>(
            name: "--log-level",
            description: "Set the minimum log level",
            getDefaultValue: () => LogLevel.Information
        );

        var rootCommand = new RootCommand("Converts Microsoft Visual C++ projects and solutions to CMake");
        rootCommand.AddOption(projectsOption);
        rootCommand.AddOption(solutionOption);
        rootCommand.AddOption(qtVersionOption);
        rootCommand.AddOption(enableStandaloneProjectBuildsOption);
        rootCommand.AddOption(dryRunOption);
        rootCommand.AddOption(logLevelOption);
        rootCommand.AddValidator(result =>
        {
            var hasProjects = result.GetValueForOption(projectsOption)?.Count > 0;
            var hasSolution = !string.IsNullOrEmpty(result.GetValueForOption(solutionOption));
            if (hasProjects == hasSolution)
            {
                result.ErrorMessage = "Specify either --projects or --solution, but not both.";
            }
        });
        rootCommand.SetHandler(
            Run, 
            projectsOption, 
            solutionOption, 
            qtVersionOption, 
            enableStandaloneProjectBuildsOption, 
            dryRunOption, 
            logLevelOption);

        var parser = new CommandLineBuilder(rootCommand)
            .UseHelp()
            .UseTypoCorrections()
            .UseParseErrorReporting()
            .UseExceptionHandler((ex, context) => HandleException(ex))
            .Build();

        return parser.Invoke(args);
    }

    static void Run(List<string>? projects, string? solution, int? qtVersion, bool enableStandaloneProjectBuilds, bool dryRun, LogLevel logLevel)
    {
        logger = CreateLogger(logLevel);

        ICMakeFileWriter writer = dryRun ? new ConsoleFileWriter(logger) : new DiskFileWriter();

        var converter = new Converter(logger);
        converter.Convert(projects, solution, qtVersion, enableStandaloneProjectBuilds, writer);
    }

    static ILogger CreateLogger(LogLevel logLevel)
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .SetMinimumLevel(logLevel)
                .AddConsole(options => options.FormatterName = nameof(CustomConsoleFormatter))
                .AddConsoleFormatter<CustomConsoleFormatter, ConsoleFormatterOptions>();    
        });

        return loggerFactory.CreateLogger("vcxproj2cmake");
    }


    static void HandleException(Exception ex)
    {
        if (ex is CatastrophicFailureException)
        {
            if (logger != null)
            {
                logger.LogCritical(ex.Message);
                logger.LogCritical("Aborting.");
            }
            else
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                Console.Error.WriteLine("Aborting.");
            }
        }
        else
        {
            if (logger != null)
                logger.LogCritical(ex, "Unexpected error");
            else
                Console.Error.WriteLine($"Unexpected error: {ex}");
        }
    }
}

public class CatastrophicFailureException : Exception
{
    public CatastrophicFailureException() { }
    public CatastrophicFailureException(string message) : base(message) { }
    public CatastrophicFailureException(string message, Exception inner) : base(message, inner) { }
}
