using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;

static class Program
{
    static int Main(string[] args)
    {
        var projectOption = new Option<List<string>?>(
            name: "--project",
            description: "Path(s) to .vcxproj file(s)")
        {
            AllowMultipleArgumentsPerToken = true
        };

        var solutionOption = new Option<string?>(
            name: "--solution",
            description: "Path to .sln file");

        var enableStandaloneProjectBuildsOption = new Option<bool>(
            name: "--enable-standalone-project-builds",
            description: "Generate necessary code to allow projects to be built standalone (not through the root CMakeLists.txt)");

        var dryRunOption = new Option<bool>(
            name: "--dry-run",
            description: "Print generated output to the console, do not store generated files");

        var rootCommand = new RootCommand("Converts Microsoft Visual C++ projects and solutions to CMake");
        rootCommand.AddOption(projectOption);
        rootCommand.AddOption(solutionOption);
        rootCommand.AddOption(enableStandaloneProjectBuildsOption);
        rootCommand.AddOption(dryRunOption);
        rootCommand.AddValidator(result =>
        {
            var hasProjects = result.GetValueForOption(projectOption)?.Count > 0;
            var hasSolution = !string.IsNullOrEmpty(result.GetValueForOption(solutionOption));
            if (hasProjects == hasSolution)
            {
                result.ErrorMessage = "Specify either --project or --solution, but not both.";
            }
        });
        rootCommand.SetHandler(Run, projectOption, solutionOption, enableStandaloneProjectBuildsOption, dryRunOption);

        var parser = new CommandLineBuilder(rootCommand)
            .UseHelp()
            .UseTypoCorrections()
            .UseParseErrorReporting()
            .UseExceptionHandler((ex, context) => { 
                if (ex is CatastrophicFailureException)
                {
                    Console.Error.WriteLine($"Error: {ex.Message}");
                    Console.Error.WriteLine("Aborting.");
                }
                else
                {
                    Console.Error.WriteLine($"Unexpected error: {ex}");
                }
            })
            .Build();

        return parser.Invoke(args);
    }

    static void Run(List<string>? projects, string? solution, bool enableStandaloneProjectBuilds, bool dryRun)
    {
        var conanPackageInfoRepository = new ConanPackageInfoRepository();

        SolutionInfo? solutionInfo = null;
        List<ProjectInfo> projectInfos = new();
        
        if (projects != null && projects.Any())
        {
            foreach (var projectPath in projects!)
            {
                projectInfos.Add(ProjectInfo.ParseProjectFile(projectPath, conanPackageInfoRepository));
            }
        }
        else if (solution != null)
        {
            solutionInfo = SolutionInfo.ParseSolutionFile(solution!);

            if (solutionInfo.Projects.Length == 0)
                throw new CatastrophicFailureException($"No .vcxproj files found in solution: {solution}");

            foreach (var projectReference in solutionInfo.Projects)
            {
                string absolutePath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(solution)!, projectReference.Path));
                projectReference.ProjectFileInfo = ProjectInfo.ParseProjectFile(absolutePath, conanPackageInfoRepository);
                projectInfos.Add(projectReference.ProjectFileInfo);
            }
        }

        AssignUniqueProjectNames(projectInfos);
        ResolveProjectReferences(projectInfos);
        projectInfos = RemoveObsoleteLibrariesFromProjectReferences(projectInfos);

        var settings = new CMakeGeneratorSettings(enableStandaloneProjectBuilds, dryRun);
        CMakeGenerator.Generate(solutionInfo, projectInfos, settings);
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
                LinkLibraryDependenciesEnabled = projectInfo.LinkLibraryDependenciesEnabled,
                RequiresMoc = projectInfo.RequiresMoc,
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