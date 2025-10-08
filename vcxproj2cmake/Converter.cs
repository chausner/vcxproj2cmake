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
        bool dryRun = false,
        bool continueOnError = false)
    {
        if ((projectFiles == null || projectFiles.Count == 0) && solutionFile == null)
            throw new ArgumentException($"Either {nameof(projectFiles)} or {nameof(solutionFile)} must be provided.");
        else if (projectFiles != null && projectFiles.Count > 0 && solutionFile != null)
            throw new ArgumentException($"Only one of {nameof(projectFiles)} or {nameof(solutionFile)} can be provided, not both.");

        MSBuildSolution? solution = null;
        List<MSBuildProject> projects = [];
        List<string> failedProjectPaths = [];

        if (projectFiles != null && projectFiles.Any())
        {
            foreach (var project in projectFiles!)
            {
                var absolutePath = Path.GetFullPath(project.FullName);
                try
                {                    
                    projects.Add(MSBuildProject.ParseProjectFile(absolutePath, fileSystem, logger));
                }
                catch (Exception ex) when (continueOnError)
                {
                    logger.LogError(ex, "Error processing project file {ProjectFile}: {ErrorMessage}", project.FullName, ex.Message);
                    failedProjectPaths.Add(absolutePath);
                }
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
                try
                {                    
                    projects.Add(MSBuildProject.ParseProjectFile(absolutePath, fileSystem, logger));
                }
                catch (Exception ex) when (continueOnError)
                {
                    logger.LogError(ex, "Error processing project file {ProjectFile}: {ErrorMessage}", projectReference, ex.Message);
                    failedProjectPaths.Add(absolutePath);
                }
            }
        }

        var conanPackageInfoRepository = new ConanPackageInfoRepository();

        List<CMakeProject> cmakeProjects = [];

        foreach (var project in projects)
        {
            try
            {
                cmakeProjects.Add(new CMakeProject(project, qtVersion, conanPackageInfoRepository, fileSystem, logger));
            }
            catch (Exception ex) when (continueOnError)
            {
                logger.LogError(ex, "Error processing project file {ProjectFile}: {ErrorMessage}", project.AbsoluteProjectPath, ex.Message);
                failedProjectPaths.Add(project.AbsoluteProjectPath);
            }
        }

        if (solution != null && solution.Projects.Length != cmakeProjects.Count)
        {
            solution = new MSBuildSolution
            {
                AbsoluteSolutionPath = solution.AbsoluteSolutionPath,
                SolutionName = solution.SolutionName,
                Projects = solution.Projects.Where(projectRef =>
                {
                    string absolutePath = Path.GetFullPath(Path.Combine(solutionFile!.DirectoryName!, projectRef));
                    return cmakeProjects.Any(p => p.AbsoluteProjectPath == absolutePath);
                }).ToArray()
            };
        }

        var cmakeSolution = solution != null ? new CMakeSolution(solution, cmakeProjects) : null;

        EnsureProjectNamesAreUnique(cmakeProjects);
        ResolveProjectReferences(cmakeProjects, continueOnError, failedProjectPaths);
        RemoveObsoleteLibrariesFromProjectReferences(cmakeProjects);
        AddLibrariesFromProjectReferences(cmakeProjects);

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

    void ResolveProjectReferences(IEnumerable<CMakeProject> projects, bool continueOnError, IEnumerable<string> failedProjectPaths)
    {
        foreach (var project in projects)
        {
            List<CMakeProjectReference> resolvedReferences = [];

            foreach (var projectReference in project.ProjectReferences)
            {
                var absoluteReference = Path.GetFullPath(projectReference.Path, Path.GetDirectoryName(project.AbsoluteProjectPath)!);

                var referencedProject = projects.FirstOrDefault(p => p.AbsoluteProjectPath == absoluteReference);

                if (referencedProject == null)
                {
                    if (continueOnError && failedProjectPaths.Contains(absoluteReference, StringComparer.OrdinalIgnoreCase))
                    {
                        logger.LogError("Skipping project reference {MissingProjectReference} in project {ProjectFile} because the referenced project could not be converted.", absoluteReference, project.AbsoluteProjectPath);
                        continue;
                    }

                    throw new CatastrophicFailureException($"Project {project.AbsoluteProjectPath} references project {absoluteReference} which is not part of the solution or the list of projects.");                    
                }

                projectReference.Project = referencedProject;
                resolvedReferences.Add(projectReference);
            }

            project.ProjectReferences = resolvedReferences.ToArray();
        }
    }

    void RemoveObsoleteLibrariesFromProjectReferences(IEnumerable<CMakeProject> projects)
    {
        foreach (var project in projects)
        {
            if (!project.MSBuildProject.LinkLibraryDependenciesEnabled)
                continue;

            var dependencyTargets = project.GetAllReferencedProjects(projects)
                .Where(project => project.TargetType is CMakeTargetType.StaticLibrary or CMakeTargetType.SharedLibrary)
                .Select(project => project.OutputName + ".lib")
                .ToArray();

            foreach (var dependencyTarget in dependencyTargets)
                if (project.Libraries.Values.Values.SelectMany(s => s).Contains(dependencyTarget, StringComparer.OrdinalIgnoreCase))
                {
                    logger!.LogInformation($"Removing explicit library dependency {dependencyTarget} from project {project.ProjectName} since LinkLibraryDependencies is enabled.");
                }

            project.Libraries = project.Libraries.Map(libraries => libraries.Except(dependencyTargets, StringComparer.OrdinalIgnoreCase).ToArray(), project.ProjectConfigurations, logger!);
        }
    }

    private static void AddLibrariesFromProjectReferences(IEnumerable<CMakeProject> cmakeProjects)
    {
        foreach (var project in cmakeProjects)
        {
            if (!project.MSBuildProject.LinkLibraryDependenciesEnabled)
                continue;

            foreach (var projectRef in ProjectDependencyUtils.OrderProjectReferencesByDependencies(project.ProjectReferences, cmakeProjects))
                if (projectRef.Project!.TargetType is CMakeTargetType.StaticLibrary or CMakeTargetType.SharedLibrary)
                    project.Libraries.AppendValue(Config.CommonConfig, projectRef.Project.ProjectName);
        }
    }
}
