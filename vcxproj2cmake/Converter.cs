using Microsoft.Extensions.Logging;
using System.IO.Abstractions;

namespace vcxproj2cmake;

public class Converter
{
    readonly IFileSystem fileSystem;
    readonly ILogger logger;

    public Converter(IFileSystem fileSystem, ILogger logger)
    {
        this.fileSystem = fileSystem;
        this.logger = logger;
    }

    public void Convert(
        List<FileInfo>? projectFiles = null, 
        FileInfo? solutionFile = null,
        int? qtVersion = null,
        bool enableStandaloneProjectBuilds = false, 
        IndentStyle indentStyle = IndentStyle.Spaces, 
        int indentSize = 4, 
        bool dryRun = false)
    {
        if ((projectFiles == null || projectFiles.Count == 0) && solutionFile == null)
            throw new ArgumentException($"Either {nameof(projectFiles)} or {nameof(solutionFile)} must be provided.");
        else if (projectFiles != null && projectFiles.Count > 0 && solutionFile != null)
            throw new ArgumentException($"Only one of {nameof(projectFiles)} or {nameof(solutionFile)} can be provided, not both.");

        MSBuildSolution? solution = null;
        List<MSBuildProject> projects = [];

        if (projectFiles != null && projectFiles.Any())
        {
            foreach (var project in projectFiles!)
            {
                projects.Add(MSBuildProject.ParseProjectFile(project.FullName, fileSystem, logger));
            }
        }
        else if (solutionFile != null)
        {
            solution = MSBuildSolution.ParseSolutionFile(solutionFile!.FullName, fileSystem, logger);

            if (solution.Projects.Length == 0)
                throw new CatastrophicFailureException($"No .vcxproj files found in solution: {solutionFile}");

            foreach (var projectReference in solution.Projects)
            {
                string absolutePath = Path.GetFullPath(Path.Combine(solutionFile.DirectoryName!, projectReference));
                projects.Add(MSBuildProject.ParseProjectFile(absolutePath, fileSystem, logger));
            }
        }

        var conanPackageInfoRepository = new ConanPackageInfoRepository();

        var cmakeProjects = projects.Select(project => new CMakeProject(project, qtVersion, conanPackageInfoRepository, fileSystem, logger)).ToList();
        var cmakeSolution = solution != null ? new CMakeSolution(solution, cmakeProjects) : null;

        EnsureProjectNamesAreUnique(cmakeProjects);
        ResolveProjectReferences(cmakeProjects);
        RemoveObsoleteLibrariesFromProjectReferences(cmakeProjects);
        foreach (var project in cmakeProjects)
            project.FinalizeLibraries(cmakeProjects);

        var settings = new CMakeGeneratorSettings(enableStandaloneProjectBuilds, indentStyle, indentSize, dryRun);
        var cmakeGenerator = new CMakeGenerator(fileSystem, logger);
        cmakeGenerator.Generate(cmakeSolution, cmakeProjects, settings);
    }

    static void EnsureProjectNamesAreUnique(IEnumerable<CMakeProject> projects)
    {
        HashSet<string> assignedNames = [];

        foreach (var project in projects)
        {
            if (!assignedNames.Add(project.ProjectName))
            {
                int i = 2;
                while (!assignedNames.Add($"{project.ProjectName}{i}"))
                    i++;
                project.ProjectName = $"{project.ProjectName}{i}";
            }
        }
    }

    static void ResolveProjectReferences(IEnumerable<CMakeProject> projects)
    {
        foreach (var project in projects)
        {
            foreach (var projectReference in project.ProjectReferences)
            {
                var absoluteReference = Path.GetFullPath(projectReference.Path, Path.GetDirectoryName(project.AbsoluteProjectPath)!);

                var referencedProject = projects.FirstOrDefault(p => p.AbsoluteProjectPath == absoluteReference);

                if (referencedProject == null)
                    throw new CatastrophicFailureException($"Project {project.AbsoluteProjectPath} references project {absoluteReference} which is not part of the solution or the list of projects.");

                projectReference.Project = referencedProject;
            }
        }
    }

    void RemoveObsoleteLibrariesFromProjectReferences(IEnumerable<CMakeProject> projects)
    {
        foreach (var project in projects)
        {
            if (!project.LinkLibraryDependenciesEnabled)
                continue;

            // Assumes that the output library names have not been customized and are the same as the project names with a .lib extension
            var dependencyTargets = project.GetAllReferencedProjects(projects)
                .Where(project => project.ConfigurationType == "StaticLibrary" || project.ConfigurationType == "DynamicLibrary")
                .Select(project => project.ProjectName + ".lib")
                .ToArray();

            foreach (var dependencyTarget in dependencyTargets)
                if (project.Libraries.Values.Values.SelectMany(s => s).Contains(dependencyTarget, StringComparer.OrdinalIgnoreCase))
                {
                    logger!.LogInformation($"Removing explicit library dependency {dependencyTarget} from project {project.ProjectName} since LinkLibraryDependencies is enabled.");
                }

            project.Libraries = project.Libraries.Map(libraries => libraries.Except(dependencyTargets, StringComparer.OrdinalIgnoreCase).ToArray(), project.ProjectConfigurations, logger!);
        }
    }
}
