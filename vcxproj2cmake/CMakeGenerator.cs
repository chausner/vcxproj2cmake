using Microsoft.Extensions.Logging;
using Scriban;
using Scriban.Runtime;
using System.IO.Abstractions;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace vcxproj2cmake;

class CMakeGenerator
{
    readonly IFileSystem fileSystem;
    readonly ILogger logger;

    public CMakeGenerator(IFileSystem fileSystem, ILogger logger)
    {
        this.fileSystem = fileSystem;
        this.logger = logger;
    }

    public void Generate(CMakeSolution? solution, IEnumerable<CMakeProject> projects, CMakeGeneratorSettings settings)
    {
        var projectCMakeListsTemplate = LoadTemplate("vcxproj2cmake.Resources.Templates.Project-CMakeLists.txt.scriban");
        var solutionCMakeListsTemplate = LoadTemplate("vcxproj2cmake.Resources.Templates.Solution-CMakeLists.txt.scriban");

        ValidateFolders(solution, projects);

        foreach (var project in projects)
            GenerateCMakeForProject(project, projects, projectCMakeListsTemplate, settings);

        if (solution != null)
            GenerateCMakeForSolution(solution, projects, solutionCMakeListsTemplate, settings);
    }

    static void ValidateFolders(CMakeSolution? solution, IEnumerable<CMakeProject> projects)
    {
        HashSet<string> folders = [];

        foreach (var project in projects)
        {
            var folder = Path.GetDirectoryName(project.AbsoluteProjectPath)!;
            if (!folders.Add(folder))
                throw new CatastrophicFailureException($"Directory {folder} contains two or more projects. This is not supported.");
        }

        if (solution != null && !folders.Add(Path.GetDirectoryName(solution.AbsoluteSolutionPath)!))
            throw new CatastrophicFailureException($"The solution file and at least one project file are located in the same directory. This is not supported.");
    }

    void GenerateCMake(object model, IEnumerable<CMakeProject> allProjects, string destinationPath, Template cmakeListsTemplate, CMakeGeneratorSettings settings)
    {
        logger.LogInformation($"Generating {destinationPath}");

        var scriptObject = new ScriptObject();
        scriptObject.Import(model);
        scriptObject.Import(settings);
        scriptObject.SetValue("all_projects", allProjects, true);
        scriptObject.Import("fail", new Action<string>(error => throw new CatastrophicFailureException(error)));
        scriptObject.Import("translate_msbuild_macros", TranslateMSBuildMacros);
        scriptObject.Import("normalize_path", NormalizePath);
        scriptObject.Import("order_project_references_by_dependencies", OrderProjectReferencesByDependencies);
        scriptObject.Import("get_directory_name", new Func<string?, string?>(Path.GetDirectoryName));
        scriptObject.Import("get_relative_path", new Func<string, string, string?>((path, relativeTo) => Path.GetRelativePath(relativeTo, path)));
        scriptObject.Import("prepend_relative_paths_with_cmake_current_source_dir", PrependRelativePathsWithCMakeCurrentSourceDir);

        var context = new TemplateContext();
        context.PushGlobal(scriptObject);
        var result = cmakeListsTemplate.Render(context);

        if (settings.IndentStyle != IndentStyle.Spaces || settings.IndentSize != 4)
            result = ApplyIndentation(result, settings.IndentStyle, settings.IndentSize);

        if (settings.DryRun)
        {
            var newline = Environment.NewLine;
            var extraIndentedResult = Regex.Replace(result, "^", "    ", RegexOptions.Multiline);
            logger.LogInformation($"Generated output for {destinationPath}:{newline}{newline}{extraIndentedResult}");
        }
        else
        {
            fileSystem.File.WriteAllText(destinationPath, result);
        }
    }

    private static string ApplyIndentation(string text, IndentStyle indentStyle, int indentSize)
    {
        return Regex.Replace(text, @"^((    )+)", match =>
        {
            var indentLevel = match.Length / 4;
            return indentStyle switch
            {
                IndentStyle.Spaces => new string(' ', indentLevel * indentSize),
                IndentStyle.Tabs => new string('\t', indentLevel),
                _ => throw new ArgumentException($"Invalid indent style: {indentStyle}")
            };
        }, RegexOptions.Multiline);
    }

    void GenerateCMakeForProject(CMakeProject project, IEnumerable<CMakeProject> allProjects, Template cmakeListsTemplate, CMakeGeneratorSettings settings)
    {
        string cmakeListsPath = Path.Combine(Path.GetDirectoryName(project.AbsoluteProjectPath)!, "CMakeLists.txt");

        GenerateCMake(project, allProjects, cmakeListsPath, cmakeListsTemplate, settings);
    }

    void GenerateCMakeForSolution(CMakeSolution solution, IEnumerable<CMakeProject> allProjects, Template cmakeListsTemplate, CMakeGeneratorSettings settings)
    {
        string cmakeListsPath = Path.Combine(Path.GetDirectoryName(solution.AbsoluteSolutionPath)!, "CMakeLists.txt");

        GenerateCMake(solution, allProjects, cmakeListsPath, cmakeListsTemplate, settings);
    }

    static Template LoadTemplate(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var streamReader = new StreamReader(stream);
        string content = streamReader.ReadToEnd();
        return Template.Parse(content);
    }

    static string NormalizePath(string path)
    {
        if (path == string.Empty)
            return string.Empty;

        // In CMake, we should always use forward-slashes as directory separator, even on Windows
        string normalizedPath = path.Replace(@"\", "/");

        // Remove duplicated separators
        normalizedPath = Regex.Replace(normalizedPath, @"//+", "/");

        // Remove ./ prefix(es)
        while (normalizedPath.StartsWith("./"))
            normalizedPath = normalizedPath[2..];
        if (normalizedPath == string.Empty)
            return ".";

        // Remove /. suffix(es)
        while (normalizedPath.EndsWith("/."))
            normalizedPath = normalizedPath[..^2];
        if (normalizedPath == string.Empty)
            return "/";

        // Remove unnecessary path components
        normalizedPath = normalizedPath.Replace("/./", "/");

        // Remove trailing separator
        if (normalizedPath.EndsWith('/') && normalizedPath != "/")
            normalizedPath = normalizedPath[..^1];

        return normalizedPath;
    }

    static string PrependRelativePathsWithCMakeCurrentSourceDir(string normalizedPath)
    {
        var isAbsolutePath = Path.IsPathRooted(normalizedPath);

        // if a path starts with a CMake variable, we just assume that the variable resolves to an absolute path
        isAbsolutePath |= normalizedPath.StartsWith("${");

        if (!isAbsolutePath)
            if (normalizedPath == ".")
                return "${CMAKE_CURRENT_SOURCE_DIR}";
            else
                return "${CMAKE_CURRENT_SOURCE_DIR}/" + normalizedPath;
        else
            return normalizedPath;
    }

    string TranslateMSBuildMacros(string value)
    {
        string translatedValue = value;
        translatedValue = Regex.Replace(translatedValue, @"\$\(Configuration(Name)?\)", "${CMAKE_BUILD_TYPE}/");
        translatedValue = Regex.Replace(translatedValue, @"\$\(ProjectDir\)[/\\]*", "${CMAKE_CURRENT_SOURCE_DIR}/");
        translatedValue = Regex.Replace(translatedValue, @"\$\(ProjectName\)", "${PROJECT_NAME}");
        translatedValue = Regex.Replace(translatedValue, @"\$\(SolutionDir\)[/\\]*", "${CMAKE_SOURCE_DIR}/");
        translatedValue = Regex.Replace(translatedValue, @"\$\(SolutionName\)", "${CMAKE_PROJECT_NAME}");

        if (Regex.IsMatch(translatedValue, @"\$\([A-Za-z0-9_]+\)"))
        {
            logger.LogWarning($"Value contains unsupported MSBuild macros/properties: {value}");
        }

        translatedValue = Regex.Replace(translatedValue, @"\$\(([A-Za-z0-9_]+)\)", "${$1}");

        return translatedValue;
    }

    CMakeProject[] OrderProjectsByDependencies(IEnumerable<CMakeProject> projects)
    {
        List<CMakeProject> orderedProjects = [];
        List<CMakeProject> unorderedProjects = projects.OrderBy(p => p.AbsoluteProjectPath).ToList();

        while (unorderedProjects.Count > 0)
        {
            var projectWithAllDependenciesSatisfied = unorderedProjects
                .FirstOrDefault(project => project.ProjectReferences.All(p => orderedProjects.Any(p2 => p2.AbsoluteProjectPath == p.Project!.AbsoluteProjectPath)));

            if (projectWithAllDependenciesSatisfied != null)
            {
                orderedProjects.Add(projectWithAllDependenciesSatisfied);
                unorderedProjects.Remove(projectWithAllDependenciesSatisfied);
            }
            else
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Could not determine project dependency tree");

                foreach (var project in unorderedProjects)
                {
                    sb.AppendLine($"Project {project.ProjectName}");
                    foreach (var missingReference in project.ProjectReferences.Where(pr =>
                                 orderedProjects.All(p => p.AbsoluteProjectPath != pr.Project!.AbsoluteProjectPath)))
                    {
                        sb.AppendLine($"  missing dependency {missingReference.Path}");
                    }
                }

                logger.LogError(sb.ToString());

                throw new CatastrophicFailureException("Could not determine project dependency tree");
            }
        }

        return orderedProjects.ToArray();
    }

    CMakeProjectReference[] OrderProjectReferencesByDependencies(IEnumerable<CMakeProjectReference> projectReferences, IEnumerable<CMakeProject>? allProjects = null)
    {
        var orderedProjects = OrderProjectsByDependencies(allProjects ?? projectReferences.Select(pr => pr.Project!));

        return projectReferences
            .OrderBy(pr => Array.FindIndex(orderedProjects, p => p.AbsoluteProjectPath == pr.Project!.AbsoluteProjectPath))
            .ToArray();
    }
}

record CMakeGeneratorSettings(bool EnableStandaloneProjectBuilds, IndentStyle IndentStyle, int IndentSize, bool DryRun);

public enum IndentStyle
{
    Spaces,
    Tabs
}