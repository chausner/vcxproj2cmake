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
            GenerateCMakeForProject(projectInfo, projectCMakeListsTemplate, settings);

        if (solutionInfo != null)
            GenerateCMakeForSolution(solutionInfo, solutionCMakeListsTemplate, settings);
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

    static void GenerateCMake(object model, string destinationPath, Template cmakeListsTemplate, CMakeGeneratorSettings settings)
    {
        var scriptObject = new ScriptObject();
        scriptObject.Import(model);
        scriptObject.Import(settings);
        scriptObject.Import("fail", new Action<string>(error => throw new CatastrophicFailureException(error)));
        scriptObject.Import("translate_msbuild_macros", TranslateMSBuildMacros);
        scriptObject.Import("normalize_path", NormalizePath);
        scriptObject.Import("order_projects_by_dependencies", OrderProjectsByDependencies);
        scriptObject.Import("get_directory_name", new Func<string?, string?>(Path.GetDirectoryName));
        scriptObject.Import("get_relative_path", new Func<string, string, string?>((path, relativeTo) => Path.GetRelativePath(relativeTo, path)));
        scriptObject.Import("prepend_relative_paths_with_cmake_current_source_dir", PrependRelativePathsWithCMakeCurrentSourceDir);

        var context = new TemplateContext();
        context.PushGlobal(scriptObject);
        var result = cmakeListsTemplate.Render(context);

        if (settings.DryRun)
        {
            Console.WriteLine($"\nGenerated output for {destinationPath}\n");
            Console.WriteLine(result);
        }
        else
        {
            Console.WriteLine($"Generating {destinationPath}");
            File.WriteAllText(destinationPath, result);
        }
    }

    static void GenerateCMakeForProject(ProjectInfo projectInfo, Template cmakeListsTemplate, CMakeGeneratorSettings settings)
    {
        string cmakeListsPath = Path.Combine(Path.GetDirectoryName(projectInfo.AbsoluteProjectPath)!, "CMakeLists.txt");

        GenerateCMake(projectInfo, cmakeListsPath, cmakeListsTemplate, settings);
    }

    static void GenerateCMakeForSolution(SolutionInfo solutionInfo, Template cmakeListsTemplate, CMakeGeneratorSettings settings)
    {
        string cmakeListsPath = Path.Combine(Path.GetDirectoryName(solutionInfo.AbsoluteSolutionPath)!, "CMakeLists.txt");

        GenerateCMake(solutionInfo, cmakeListsPath, cmakeListsTemplate, settings);
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
        if (!Path.IsPathRooted(normalizedPath) && !normalizedPath.StartsWith("${CMAKE_CURRENT_SOURCE_DIR}/"))
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

    static ProjectReference[] OrderProjectsByDependencies(ProjectReference[] projectReferences)
    {
        List<ProjectReference> orderedProjectReferences = new();
        List<ProjectReference> unorderedProjectReferences = new(projectReferences);

        while (unorderedProjectReferences.Count > 0)
        {
            bool found = false;

            foreach (var projectReference in unorderedProjectReferences)
            {
                if (projectReference.ProjectFileInfo!.ProjectReferences.All(pr => orderedProjectReferences.Any(pr2 => pr2.ProjectFileInfo == pr.ProjectFileInfo)))
                {
                    orderedProjectReferences.Add(projectReference);
                    unorderedProjectReferences.Remove(projectReference);
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                Console.Error.WriteLine("Could not determine project dependency tree");
                foreach (var projectReference in unorderedProjectReferences)
                {
                    Console.Error.WriteLine("  " + projectReference.Path);
                    foreach (var missingReference in projectReference.ProjectFileInfo!.ProjectReferences.Where(pr =>
                                 orderedProjectReferences.All(pr2 => pr2.ProjectFileInfo != pr.ProjectFileInfo)))
                    {
                        Console.Error.WriteLine("    missing dependency " + missingReference.Path);
                    }
                }

                //throw new CatastrophicFailureException("Could not determine project dependency tree");
                orderedProjectReferences.AddRange(unorderedProjectReferences);
                break;
            }
        }

        return orderedProjectReferences.ToArray();
    }
}

record CMakeGeneratorSettings(bool EnableStandaloneProjectBuilds, bool DryRun);