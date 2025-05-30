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
                result.ErrorMessage = "Specify either --project or --solution, but not both.";
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
            .UseExceptionHandler((ex, context) => {
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
            })
            .Build();

        return parser.Invoke(args);
    }

    static void Run(List<string>? projects, string? solution, int? qtVersion, bool enableStandaloneProjectBuilds, bool dryRun, LogLevel logLevel)
    {
        logger = CreateLogger(logLevel);

        var conanPackageInfoRepository = new ConanPackageInfoRepository();

        SolutionInfo? solutionInfo = null;
        List<ProjectInfo> projectInfos = new();
        
        if (projects != null && projects.Any())
        {
            foreach (var projectPath in projects!)
            {
                projectInfos.Add(ProjectInfo.ParseProjectFile(projectPath, qtVersion, conanPackageInfoRepository, logger));
            }
        }
        else if (solution != null)
        {
            solutionInfo = SolutionInfo.ParseSolutionFile(solution!, logger);

            if (solutionInfo.Projects.Length == 0)
                throw new CatastrophicFailureException($"No .vcxproj files found in solution: {solution}");

            foreach (var projectReference in solutionInfo.Projects)
            {
                string absolutePath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(solution)!, projectReference.Path));
                projectReference.ProjectFileInfo = ProjectInfo.ParseProjectFile(absolutePath, qtVersion, conanPackageInfoRepository, logger);
                projectInfos.Add(projectReference.ProjectFileInfo);
            }
        }

        AssignUniqueProjectNames(projectInfos);
        ResolveProjectReferences(projectInfos);
        projectInfos = RemoveObsoleteLibrariesFromProjectReferences(projectInfos);

        var settings = new CMakeGeneratorSettings(enableStandaloneProjectBuilds, dryRun);
        var cmakeGenerator = new CMakeGenerator(logger);
        cmakeGenerator.Generate(solutionInfo, projectInfos, settings);
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

    static void AssignUniqueProjectNames(IEnumerable<ProjectInfo> projectInfos)
    {
        HashSet<string> assignedNames = [];

        foreach (var projectInfo in projectInfos)
        {
            if (assignedNames.Add(projectInfo.ProjectName))
            {
                projectInfo.UniqueName = projectInfo.ProjectName;
            }
            else
            {
                int i = 2;
                while (!assignedNames.Add($"{projectInfo.ProjectName}{i}"))
                    i++;
                projectInfo.UniqueName = $"{projectInfo.ProjectName}{i}";
            }
        }
    }

    static void ResolveProjectReferences(IEnumerable<ProjectInfo> projectInfos)
    {
        foreach (var projectInfo in projectInfos)
        {
            foreach (var projectReference in projectInfo.ProjectReferences)
            {
                var absoluteReference = Path.GetFullPath(projectReference.Path, Path.GetDirectoryName(projectInfo.AbsoluteProjectPath)!);

                var referencedProjectFileInfo = projectInfos.FirstOrDefault(p => p.AbsoluteProjectPath == absoluteReference);

                if (referencedProjectFileInfo == null)
                    throw new CatastrophicFailureException($"Project {projectInfo.AbsoluteProjectPath} references project {absoluteReference} which is not part of the solution or the list of projects.");

                projectReference.ProjectFileInfo = referencedProjectFileInfo;
            }
        }
    }

    static List<ProjectInfo> RemoveObsoleteLibrariesFromProjectReferences(IEnumerable<ProjectInfo> projectInfos)
    {
        return projectInfos.Select(projectInfo =>
        {
            if (!projectInfo.LinkLibraryDependenciesEnabled)
                return projectInfo;

            // Assumes that the output library names have not been customized and are the same as the project names with a .lib extension
            var dependencyTargets = projectInfo.GetAllReferencedProjects(projectInfos)
                .Where(project => project.ConfigurationType == "StaticLibrary" || project.ConfigurationType == "DynamicLibrary")
                .Select(project => project.ProjectName + ".lib")
                .ToArray();

            foreach (var dependencyTarget in dependencyTargets)
                if (projectInfo.Libraries.Common.Contains(dependencyTarget, StringComparer.OrdinalIgnoreCase) ||
                    projectInfo.Libraries.Debug.Contains(dependencyTarget, StringComparer.OrdinalIgnoreCase) ||
                    projectInfo.Libraries.Release.Contains(dependencyTarget, StringComparer.OrdinalIgnoreCase) ||
                    projectInfo.Libraries.X86.Contains(dependencyTarget, StringComparer.OrdinalIgnoreCase) ||
                    projectInfo.Libraries.X64.Contains(dependencyTarget, StringComparer.OrdinalIgnoreCase))
                {
                    logger!.LogInformation($"Removing explicit library dependency {dependencyTarget} from project {projectInfo.ProjectName} since LinkLibraryDependencies is enabled.");
                }

            var filteredLibraries = projectInfo.Libraries.Map(libraries => libraries.Except(dependencyTargets, StringComparer.OrdinalIgnoreCase).ToArray());

            return new ProjectInfo
            {
                AbsoluteProjectPath = projectInfo.AbsoluteProjectPath,
                ProjectName = projectInfo.ProjectName,
                UniqueName = projectInfo.UniqueName,
                Languages = projectInfo.Languages,
                ConfigurationType = projectInfo.ConfigurationType,
                LanguageStandard = projectInfo.LanguageStandard,
                SourceFiles = projectInfo.SourceFiles,
                IncludePaths = projectInfo.IncludePaths,
                LinkerPaths = projectInfo.LinkerPaths,
                Libraries = filteredLibraries,
                Defines = projectInfo.Defines,
                Options = projectInfo.Options,
                ProjectReferences = projectInfo.ProjectReferences,
                LinkerSubsystem = projectInfo.LinkerSubsystem,
                LinkLibraryDependenciesEnabled = projectInfo.LinkLibraryDependenciesEnabled,
                QtVersion = projectInfo.QtVersion,
                RequiresQtMoc = projectInfo.RequiresQtMoc,
                RequiresQtUic = projectInfo.RequiresQtUic,
                RequiresQtRcc = projectInfo.RequiresQtRcc,
                QtModules = projectInfo.QtModules,
                ConanPackages = projectInfo.ConanPackages
            };            
        }).ToList();
    }
}

public class CatastrophicFailureException : Exception
{
    public CatastrophicFailureException() { }
    public CatastrophicFailureException(string message) : base(message) { }
    public CatastrophicFailureException(string message, Exception inner) : base(message, inner) { }
}
