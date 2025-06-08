using Microsoft.Extensions.Logging;

namespace vcxproj2cmake;

public class Converter
{
    readonly ILogger logger;

    public Converter(ILogger logger)
    {
        this.logger = logger;
    }

    public void Convert(List<FileInfo>? projects, FileInfo? solution, int? qtVersion, bool enableStandaloneProjectBuilds, string indentStyle, int indentSize, ICMakeFileWriter writer)
    {
        var conanPackageInfoRepository = new ConanPackageInfoRepository();

        SolutionInfo? solutionInfo = null;
        List<ProjectInfo> projectInfos = [];

        if (projects != null && projects.Any())
        {
            foreach (var project in projects!)
            {
                projectInfos.Add(ProjectInfo.ParseProjectFile(project.FullName, qtVersion, conanPackageInfoRepository, logger));
            }
        }
        else if (solution != null)
        {
            solutionInfo = SolutionInfo.ParseSolutionFile(solution!.FullName, logger);

            if (solutionInfo.Projects.Length == 0)
                throw new CatastrophicFailureException($"No .vcxproj files found in solution: {solution}");

            foreach (var projectReference in solutionInfo.Projects)
            {
                string absolutePath = Path.GetFullPath(Path.Combine(solution.DirectoryName!, projectReference.Path));
                projectReference.ProjectInfo = ProjectInfo.ParseProjectFile(absolutePath, qtVersion, conanPackageInfoRepository, logger);
                projectInfos.Add(projectReference.ProjectInfo);
            }
        }

        AssignUniqueProjectNames(projectInfos);
        ResolveProjectReferences(projectInfos);
        projectInfos = RemoveObsoleteLibrariesFromProjectReferences(projectInfos);

        var settings = new CMakeGeneratorSettings(enableStandaloneProjectBuilds, indentStyle, indentSize, writer);
        var cmakeGenerator = new CMakeGenerator(logger);
        cmakeGenerator.Generate(solutionInfo, projectInfos, settings);
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

                var referencedProjectInfo = projectInfos.FirstOrDefault(p => p.AbsoluteProjectPath == absoluteReference);

                if (referencedProjectInfo == null)
                    throw new CatastrophicFailureException($"Project {projectInfo.AbsoluteProjectPath} references project {absoluteReference} which is not part of the solution or the list of projects.");

                projectReference.ProjectInfo = referencedProjectInfo;
            }
        }
    }

    List<ProjectInfo> RemoveObsoleteLibrariesFromProjectReferences(IEnumerable<ProjectInfo> projectInfos)
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
                if (projectInfo.Libraries.Values.Values.SelectMany(s => s).Contains(dependencyTarget, StringComparer.OrdinalIgnoreCase))
                {
                    logger!.LogInformation($"Removing explicit library dependency {dependencyTarget} from project {projectInfo.ProjectName} since LinkLibraryDependencies is enabled.");
                }

            var filteredLibraries = projectInfo.Libraries.Map(libraries => libraries.Except(dependencyTargets, StringComparer.OrdinalIgnoreCase).ToArray(), projectInfo.ProjectConfigurations, logger!);

            return new ProjectInfo
            {
                AbsoluteProjectPath = projectInfo.AbsoluteProjectPath,
                ProjectName = projectInfo.ProjectName,
                UniqueName = projectInfo.UniqueName,
                ProjectConfigurations = projectInfo.ProjectConfigurations,
                Languages = projectInfo.Languages,
                ConfigurationType = projectInfo.ConfigurationType,
                CppLanguageStandard = projectInfo.CppLanguageStandard,
                CLanguageStandard = projectInfo.CLanguageStandard,
                SourceFiles = projectInfo.SourceFiles,
                IncludePaths = projectInfo.IncludePaths,
                PublicIncludePaths = projectInfo.PublicIncludePaths,
                LinkerPaths = projectInfo.LinkerPaths,
                Libraries = filteredLibraries,
                Defines = projectInfo.Defines,
                Options = projectInfo.Options,
                ProjectReferences = projectInfo.ProjectReferences,
                LinkerSubsystem = projectInfo.LinkerSubsystem,
                LinkLibraryDependenciesEnabled = projectInfo.LinkLibraryDependenciesEnabled,
                IsHeaderOnlyLibrary = projectInfo.IsHeaderOnlyLibrary,
                PrecompiledHeaderFile = projectInfo.PrecompiledHeaderFile,
                UsesOpenMP = projectInfo.UsesOpenMP,
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
