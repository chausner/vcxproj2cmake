using Scriban;
using Scriban.Runtime;
using System.Reflection;
using System.Text.RegularExpressions;

class CMakeGenerator
{
    public static void Generate(SolutionInfo? solutionInfo, IEnumerable<ProjectInfo> projectInfos, CMakeGeneratorSettings settings)
    {
        var projectCMakeListsTemplate = LoadTemplate("vcxproj2cmake.Resources.Templates.Project-CMakeLists.txt.scriban");
        var solutionCMakeListsTemplate = LoadTemplate("vcxproj2cmake.Resources.Templates.Solution-CMakeLists.txt.scriban");

        ValidateFolders(solutionInfo, projectInfos);

        foreach (var projectInfo in projectInfos)
            GenerateCMakeForProject(projectInfo, projectInfos, projectCMakeListsTemplate, settings);

        if (solutionInfo != null)
            GenerateCMakeForSolution(solutionInfo, projectInfos, solutionCMakeListsTemplate, settings);
    }

    static void ValidateFolders(SolutionInfo? solutionInfo, IEnumerable<ProjectInfo> projectInfos)
    {
        HashSet<string> folders = new();

        foreach (var projectInfo in projectInfos)
        {
            var folder = Path.GetDirectoryName(projectInfo.AbsoluteProjectPath)!;
            if (!folders.Add(folder))
                throw new CatastrophicFailureException($"Directory {folder} contains two or more projects. This is not supported.");
        }

        if (solutionInfo != null && !folders.Add(Path.GetDirectoryName(solutionInfo.AbsoluteSolutionPath)!))
            throw new CatastrophicFailureException($"The solution file and at least one project file are located in the same directory. This is not supported.");
    }

    static void GenerateCMake(object model, IEnumerable<ProjectInfo> allProjectInfos, string destinationPath, Template cmakeListsTemplate, CMakeGeneratorSettings settings)
    {
        Console.WriteLine($"Generating {destinationPath}");

        var scriptObject = new ScriptObject();
        scriptObject.Import(model);
        scriptObject.Import(settings);
        scriptObject.Import(new { AllProjects = allProjectInfos });
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

        if (settings.DryRun)
        {
            Console.WriteLine($"Generated output for {destinationPath}:\n");
            Console.WriteLine(result);
            Console.WriteLine();
        }
        else
        {            
            File.WriteAllText(destinationPath, result);
        }
    }

    static void GenerateCMakeForProject(ProjectInfo projectInfo, IEnumerable<ProjectInfo> allProjectInfos, Template cmakeListsTemplate, CMakeGeneratorSettings settings)
    {
        string cmakeListsPath = Path.Combine(Path.GetDirectoryName(projectInfo.AbsoluteProjectPath)!, "CMakeLists.txt");

        GenerateCMake(projectInfo, allProjectInfos, cmakeListsPath, cmakeListsTemplate, settings);
    }

    static void GenerateCMakeForSolution(SolutionInfo solutionInfo, IEnumerable<ProjectInfo> allProjectInfos, Template cmakeListsTemplate, CMakeGeneratorSettings settings)
    {
        string cmakeListsPath = Path.Combine(Path.GetDirectoryName(solutionInfo.AbsoluteSolutionPath)!, "CMakeLists.txt");

        GenerateCMake(solutionInfo, allProjectInfos, cmakeListsPath, cmakeListsTemplate, settings);
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

    static string TranslateMSBuildMacros(string value)
    {
        string translatedValue = value;
        translatedValue = Regex.Replace(translatedValue, @"\$\(Configuration(Name)?\)", "${CMAKE_BUILD_TYPE}/");
        translatedValue = Regex.Replace(translatedValue, @"\$\(ProjectDir\)[/\\]*", "${CMAKE_CURRENT_SOURCE_DIR}/");
        translatedValue = Regex.Replace(translatedValue, @"\$\(ProjectName\)", "${PROJECT_NAME}");
        translatedValue = Regex.Replace(translatedValue, @"\$\(SolutionDir\)[/\\]*", "${CMAKE_SOURCE_DIR}/");
        translatedValue = Regex.Replace(translatedValue, @"\$\(SolutionName\)", "${CMAKE_PROJECT_NAME}");

        if (Regex.IsMatch(translatedValue, @"\$\([A-Za-z0-9_]+\)"))
        {
            Console.WriteLine($"Warning: value contains unsupported MSBuild macros/properties: {value}");
        }

        translatedValue = Regex.Replace(translatedValue, @"\$\(([A-Za-z0-9_]+)\)", "${$1}");

        return translatedValue;
    }

    static ProjectInfo[] OrderProjectsByDependencies(IEnumerable<ProjectInfo> projects)
    {
        List<ProjectInfo> orderedProjects = new();
        List<ProjectInfo> unorderedProjects = new(projects);

        while (unorderedProjects.Count > 0)
        {
            var projectsWithAllDependenciesSatisfied = unorderedProjects
                .Where(project => project.ProjectReferences.All(p => orderedProjects.Any(p2 => p2.AbsoluteProjectPath == p.ProjectFileInfo!.AbsoluteProjectPath)))
                .ToArray();

            if (projectsWithAllDependenciesSatisfied.Length > 0)
            {
                foreach (var project in projectsWithAllDependenciesSatisfied.OrderBy(p => p.AbsoluteProjectPath))
                {
                    orderedProjects.Add(project);
                    unorderedProjects.Remove(project);
                }
            }
            else
            {
                Console.Error.WriteLine("Could not determine project dependency tree");
                foreach (var project in unorderedProjects)
                {
                    Console.Error.WriteLine("  " + project.ProjectName);
                    foreach (var missingReference in project.ProjectReferences.Where(pr =>
                                 orderedProjects.All(p => p.AbsoluteProjectPath != pr.ProjectFileInfo!.AbsoluteProjectPath)))
                    {
                        Console.Error.WriteLine("    missing dependency " + missingReference.Path);
                    }
                }

                throw new CatastrophicFailureException("Could not determine project dependency tree");
            }
        }

        return orderedProjects.ToArray();
    }

    static ProjectReference[] OrderProjectReferencesByDependencies(IEnumerable<ProjectReference> projectReferences, IEnumerable<ProjectInfo>? allProjects = null)
    {
        var orderedProjects = OrderProjectsByDependencies(allProjects ?? projectReferences.Select(pr => pr.ProjectFileInfo!));

        return projectReferences
            .OrderBy(pr => Array.FindIndex(orderedProjects, p => p.AbsoluteProjectPath == pr.ProjectFileInfo!.AbsoluteProjectPath))
            .ToArray();
    }
}

record CMakeGeneratorSettings(bool EnableStandaloneProjectBuilds, bool DryRun);