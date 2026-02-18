using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using System.CommandLine;
using System.Globalization;
using System.IO.Abstractions;

namespace vcxproj2cmake;

public static class Program
{
    static ILogger? logger;

    public static int Main(string[] args)
    {
        var englishCulture = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.CurrentCulture = englishCulture;
        CultureInfo.DefaultThreadCurrentCulture = englishCulture;
        CultureInfo.CurrentUICulture = englishCulture;
        CultureInfo.DefaultThreadCurrentUICulture = englishCulture;

        var projectsOption = new Option<List<FileInfo>>("--projects")
        {
            AllowMultipleArgumentsPerToken = true,
            Description = "Paths to one or multiple .vcxproj files",
            HelpName = "path(s)"
        }.AcceptExistingOnly();

        var solutionOption = new Option<FileInfo>("--solution")
        {
            Description = "Path to a solution .sln file",
            HelpName = "path"
        }.AcceptExistingOnly();

        var qtVersionOption = new Option<int?>("--qt-version") 
        { 
            Description = "Set Qt version (required for Qt projects)" 
        }.AcceptOnlyFromAmong("5", "6");

        var includeHeadersOption = new Option<bool>("--include-headers")
        {
            Description = "Include header files in target_sources(...)"
        };

        var enableStandaloneProjectBuildsOption = new Option<bool>("--enable-standalone-project-builds")
        {
            Description = "Generate necessary code to allow projects to be built standalone (not through the root CMakeLists.txt)"
        };

        var indentStyleOption = new Option<IndentStyle>("--indent-style")
        {
            Description = "The indentation style to use (spaces or tabs).",
            DefaultValueFactory = _ => IndentStyle.Spaces,
        };

        var indentSizeOption = new Option<int>("--indent-size")
        {
            Description = "The number of spaces to use for indentation.",
            HelpName = "count",
            DefaultValueFactory = _ => 4
        };

        var dryRunOption = new Option<bool>("--dry-run")
        {
            Description = "Print generated output to the console, do not store generated files"
        };

        var continueOnErrorOption = new Option<bool>("--continue-on-error")
        {
            Description = "Do not abort when a project cannot be converted and continue with the remaining projects"
        };

        var logLevelOption = new Option<LogLevel>("--log-level")
        { 
            Description = "Set the minimum log level",
            DefaultValueFactory = _ => LogLevel.Information
        };

        var rootCommand = new RootCommand("Converts Microsoft Visual C++ projects and solutions to CMake");

        rootCommand.Options.Add(projectsOption);
        rootCommand.Options.Add(solutionOption);
        rootCommand.Options.Add(qtVersionOption);
        rootCommand.Options.Add(includeHeadersOption);
        rootCommand.Options.Add(enableStandaloneProjectBuildsOption);
        rootCommand.Options.Add(indentStyleOption);
        rootCommand.Options.Add(indentSizeOption);
        rootCommand.Options.Add(dryRunOption);
        rootCommand.Options.Add(continueOnErrorOption);
        rootCommand.Options.Add(logLevelOption);

        rootCommand.Validators.Add(result =>
        {
            var hasProjects = result.GetValue(projectsOption)?.Count > 0;
            var hasSolution = result.GetValue(solutionOption) != null;

            if (hasProjects == hasSolution)
                result.AddError("Specify either --projects or --solution, but not both.");
        });

        rootCommand.SetAction(parseResult =>
            {
                var projects = parseResult.GetValue(projectsOption);
                var solution = parseResult.GetValue(solutionOption);
                var qtVersion = parseResult.GetValue(qtVersionOption);
                var includeHeaders = parseResult.GetValue(includeHeadersOption);
                var enableStandaloneProjectBuilds = parseResult.GetValue(enableStandaloneProjectBuildsOption);
                var indentStyle = parseResult.GetValue(indentStyleOption);
                var indentSize = parseResult.GetValue(indentSizeOption);
                var dryRun = parseResult.GetValue(dryRunOption);
                var continueOnError = parseResult.GetValue(continueOnErrorOption);
                var logLevel = parseResult.GetValue(logLevelOption);
                Run(projects, solution, qtVersion, includeHeaders, enableStandaloneProjectBuilds, indentStyle, indentSize, dryRun, continueOnError, logLevel);
            });

        try
        {
            return rootCommand.Parse(args).Invoke(new InvocationConfiguration
            {
                EnableDefaultExceptionHandler = false
            });
        }
        catch (Exception ex)
        {
            HandleException(ex);
            return 1;
        }
    }

    static void Run(
        List<FileInfo>? projects, 
        FileInfo? solution, 
        int? qtVersion, 
        bool includeHeaders,
        bool enableStandaloneProjectBuilds, 
        IndentStyle indentStyle, 
        int indentSize, 
        bool dryRun, 
        bool continueOnError,
        LogLevel logLevel)
    {
        logger = CreateLogger(logLevel);

        var converter = new Converter(new FileSystem(), logger);
        converter.Convert(projects, solution, qtVersion, includeHeaders, enableStandaloneProjectBuilds, indentStyle, indentSize, dryRun, continueOnError);
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
