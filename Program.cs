using Scriban;
using Scriban.Runtime;
using System.CommandLine;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;

static class Program
{
    static int Main(string[] args)
    {
        var projectOption = new Option<List<string>?>(
            name: "--project",
            description: "Path(s) to .vcxproj file(s)");
        projectOption.AllowMultipleArgumentsPerToken = true;

        var solutionOption = new Option<string?>(
            name: "--solution",
            description: "Path to .sln file");

        var dryRunOption = new Option<bool>(
            name: "--dry-run",
            description: "Print generated output to the console, do not store generated files");

        var rootCommand = new RootCommand("Converts Microsoft Visual C++ projects and solutions to CMake");
        rootCommand.AddOption(projectOption);
        rootCommand.AddOption(solutionOption);
        rootCommand.AddOption(dryRunOption);
        rootCommand.SetHandler(Run, projectOption, solutionOption, dryRunOption);

        return rootCommand.Invoke(args);
    }

    static void Run(List<string>? projects, string? solution, bool dryRun)
    {
        bool hasProjects = projects != null && projects.Count > 0;
        bool hasSolution = !string.IsNullOrEmpty(solution);
        if (hasProjects == hasSolution)
        {
            Console.Error.WriteLine("Error: Specify either --project or --solution, but not both.");
            Environment.Exit(1);
        }

        var conanPackageInfo = LoadConanPackageInfo();

        SolutionFileInfo? solutionFileInfo = null;
        List<ProjectFileInfo> projectFileInfos = new();

        if (hasProjects)
        {
            foreach (var projectPath in projects!)
            {
                projectFileInfos.Add(ProjectFileInfo.ParseProjectFile(projectPath, conanPackageInfo));
            }
        }
        else if (hasSolution)
        {
            solutionFileInfo = SolutionFileInfo.ParseSolutionFile(solution!);

            if (solutionFileInfo.Projects.Length == 0)
            {
                Console.Error.WriteLine($"Error: No .vcxproj files found in solution: {solution}");
                Environment.Exit(1);
            }

            foreach (var projectReference in solutionFileInfo.Projects)
            {
                string absolutePath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(solution)!, projectReference.Path));
                projectReference.ProjectFileInfo = ProjectFileInfo.ParseProjectFile(absolutePath, conanPackageInfo);
                projectFileInfos.Add(projectReference.ProjectFileInfo);
            }
        }

        ValidateFolders(solutionFileInfo, projectFileInfos);

        ResolveProjectReferences(projectFileInfos);

        var projectCMakeListsTemplate = LoadTemplate("vcxproj2cmake.Resources.Templates.Project-CMakeLists.txt.scriban");
        var solutionCMakeListsTemplate = LoadTemplate("vcxproj2cmake.Resources.Templates.Solution-CMakeLists.txt.scriban");

        foreach (var projectFileInfo in projectFileInfos)
            GenerateCMakeForProject(projectFileInfo, projectCMakeListsTemplate, dryRun);

        if (solutionFileInfo != null)
            GenerateCMakeForSolution(solutionFileInfo, solutionCMakeListsTemplate, dryRun);
    }

    static void ValidateFolders(SolutionFileInfo? solutionFileInfo, List<ProjectFileInfo> projectFileInfos)
    {
        HashSet<string> folders = new();

        foreach (var projectFileInfo in projectFileInfos)
        {
            var folder = Path.GetDirectoryName(projectFileInfo.AbsoluteProjectPath)!;
            if (!folders.Add(folder))
            {
                Console.Error.WriteLine($"Error: Directory {folder} contains two or more projects. This is not supported.");
                Environment.Exit(1);
            }
        }

        if (solutionFileInfo != null && !folders.Add(Path.GetDirectoryName(solutionFileInfo.AbsoluteSolutionPath)!))
        {
            Console.Error.WriteLine($"Error: The solution file and at least one project file are located in the same directory. This is not supported.");
            Environment.Exit(1);
        }
    }

    static void ResolveProjectReferences(List<ProjectFileInfo> projectFileInfos)
    {
        foreach (var projectFileInfo in projectFileInfos)
        {
            foreach (var projectReference in projectFileInfo.ProjectReferences)
            {
                var absoluteReference = Path.GetFullPath(projectReference.Path, Path.GetDirectoryName(projectFileInfo.AbsoluteProjectPath)!);

                var referencedProjectFileInfo = projectFileInfos.FirstOrDefault(p => p.AbsoluteProjectPath == absoluteReference);

                if (referencedProjectFileInfo == null)
                {
                    Console.Error.WriteLine($"Error: Project {projectFileInfo.AbsoluteProjectPath} references project {absoluteReference} which is not part of the solution or the list of projects.");
                    Environment.Exit(1);
                }

                projectReference.ProjectFileInfo = referencedProjectFileInfo;
            }
        }
    }

    static void GenerateCMake(object model, string destinationPath, Template cmakeListsTemplate, bool dryRun)
    {
        var scriptObject = new ScriptObject();
        scriptObject.Import(model);
        scriptObject.Import("fail", new Action<string>(error => throw new Exception(error)));
        scriptObject.Import("translate_msbuild_macros", TranslateMSBuildMacros);
        scriptObject.Import("normalize_path", NormalizePath);
        scriptObject.Import("order_projects_by_dependencies", OrderProjectsByDependencies);
        scriptObject.Import("get_directory_name", new Func<string?, string?>(Path.GetDirectoryName));

        var context = new TemplateContext();
        context.PushGlobal(scriptObject);
        var result = cmakeListsTemplate.Render(context);

        if (dryRun)
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

    static void GenerateCMakeForProject(ProjectFileInfo projectFileInfo, Template cmakeListsTemplate, bool dryRun)
    {
        string cmakeListsPath = Path.Combine(Path.GetDirectoryName(projectFileInfo.AbsoluteProjectPath)!, "CMakeLists.txt");

        GenerateCMake(projectFileInfo, cmakeListsPath, cmakeListsTemplate, dryRun);       
    }

    static void GenerateCMakeForSolution(SolutionFileInfo solutionFileInfo, Template cmakeListsTemplate, bool dryRun)
    {
        string cmakeListsPath = Path.Combine(Path.GetDirectoryName(solutionFileInfo.AbsoluteSolutionPath)!, "CMakeLists.txt");

        GenerateCMake(solutionFileInfo, cmakeListsPath, cmakeListsTemplate, dryRun);
    }

    static Dictionary<string, ConanPackage> LoadConanPackageInfo()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("vcxproj2cmake.Resources.conan-packages.csv");
        using var streamReader = new StreamReader(stream);

        return
            streamReader.ReadToEnd()
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split(','))
            .Select(tokens => (tokens[0], new ConanPackage(tokens[1], tokens[2])))
            .ToDictionary();
    }

    static Template LoadTemplate(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();        
        using var stream = assembly.GetManifestResourceStream(resourceName);
        using var streamReader = new StreamReader(stream);
        string content = streamReader.ReadToEnd();
        return Template.Parse(content);
    }

    static string NormalizePath(string path)
    {
        // In CMake, we should always use forward-slashes as directory separator, even on Windows
        string normalizedPath = path.Replace(@"\", "/");

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

        if (Regex.Match(translatedValue, @"\$\([A-Za-z0-9_]+\)").Success)
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
                Environment.Exit(1);
            }
        }

        return orderedProjectReferences.ToArray();
    }
}

class SolutionFileInfo
{
    public required string AbsoluteSolutionPath { get; init; }
    public required string SolutionName { get; init; }
    public required ProjectReference[] Projects { get; init; }

    public static SolutionFileInfo ParseSolutionFile(string solutionPath)
    {
        Console.WriteLine($"Parsing {solutionPath}");

        var projectPaths = new List<string>();
        var regex = new Regex(@"Project\(.*?\) = .*?, ""(.*?\.vcxproj)""", RegexOptions.IgnoreCase);

        foreach (var line in File.ReadLines(solutionPath))
        {
            var match = regex.Match(line);
            if (match.Success)
                projectPaths.Add(match.Groups[1].Value);
        }

        return new SolutionFileInfo
        {
            AbsoluteSolutionPath = Path.GetFullPath(solutionPath),
            SolutionName = Path.GetFileNameWithoutExtension(solutionPath),
            Projects = projectPaths.Select(p => new ProjectReference { Path = p }).ToArray()
        };
    }
}

class ProjectFileInfo
{
    public required string AbsoluteProjectPath { get; init; }
    public required string ProjectName { get; init; }
    public required string[] Languages { get; init; }
    public required string ConfigurationType { get; init; }
    public required string? LanguageStandard { get; init; }
    public required string[] SourceFiles { get; init; }
    public required ConfigDependentSetting IncludePaths { get; init; }
    public required ConfigDependentSetting Libraries { get; init; }
    public required ConfigDependentSetting Defines { get; init; }
    public required ConfigDependentSetting Options { get; init; }
    public required ProjectReference[] ProjectReferences { get; init; }
    public required string[] QtModules { get; init; }
    public required ConanPackage[] ConanPackages { get; init; }

    public static ProjectFileInfo ParseProjectFile(string projectPath, Dictionary<string, ConanPackage> conanPackageInfo)
    {
        Console.WriteLine($"Parsing {projectPath}");

        var msbuildNamespace = "http://schemas.microsoft.com/developer/msbuild/2003";
        var projectXName = XName.Get("Project", msbuildNamespace);
        var propertyGroupXName = XName.Get("PropertyGroup", msbuildNamespace);
        var configurationTypeXName = XName.Get("ConfigurationType", msbuildNamespace);
        var itemGroupXName = XName.Get("ItemGroup", msbuildNamespace);
        var clCompileXName = XName.Get("ClCompile", msbuildNamespace);
        var projectConfigurationXName = XName.Get("ProjectConfiguration", msbuildNamespace);
        var itemDefinitionGroupXName = XName.Get("ItemDefinitionGroup", msbuildNamespace);
        var linkXName = XName.Get("Link", msbuildNamespace);
        var qtModulesXName = XName.Get("QtModules", msbuildNamespace);
        var importXName = XName.Get("Import", msbuildNamespace);
        var importGroupXName = XName.Get("ImportGroup", msbuildNamespace);
        var projectReferenceXName = XName.Get("ProjectReference", msbuildNamespace);

        var doc = XDocument.Load(projectPath);
        var projectElement = doc.Element(projectXName);

        var projectConfigurations =
            projectElement
            .Elements(itemGroupXName)
            .SelectMany(group => group.Elements(projectConfigurationXName))
            .Select(element => element.Attribute("Include")!.Value)
            .ToList();

        var configurationType =
            projectElement
            .Elements(propertyGroupXName)
            .SelectMany(group => group.Elements(configurationTypeXName))
            .Select(element => element.Value)
            .Distinct()
            .Single();

        var sourceFiles =
            projectElement
            .Elements(itemGroupXName)
            .SelectMany(group => group.Elements(clCompileXName))
            .Select(element => element.Attribute("Include")!.Value)
            .ToList();

        var qtModules =
            (projectElement
            .Elements(propertyGroupXName)
            .SelectMany(group => group.Elements(qtModulesXName))
            .Select(element => element.Value)
            .Distinct()
            .SingleOrDefault() ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var imports =
            projectElement
            .Elements(importXName)
            .Concat(projectElement.Elements(importGroupXName).SelectMany(group => group.Elements(importXName)))
            .Select(import => import.Attribute("Project")!.Value)
            .ToList();

        var projectReferences =
            projectElement
            .Elements(itemGroupXName)
            .SelectMany(group => group.Elements(projectReferenceXName))
            .Select(element => element.Attribute("Include")!.Value)
            .Distinct()
            .ToList();

        Dictionary<string, Dictionary<string, string>> compilerSettings = [];
        Dictionary<string, Dictionary<string, string>> linkerSettings = [];

        foreach (var projectConfig in projectConfigurations)
        {
            if (!Regex.IsMatch(projectConfig, @"(Debug|Release)\|(Win32|x86|x64)"))
            {
                Console.WriteLine($"Warning: skipping unsupported project configuration: {projectConfig}");
                continue;
            }

            var itemDefinitionGroups =
                projectElement
                    .Elements(itemDefinitionGroupXName)
                    .Where(group => group.Attribute("Condition") == null ||
                                    Regex.IsMatch(group.Attribute("Condition")!.Value,
                                        $@"'\$\(Configuration\)\|\$\(Platform\)'\s*==\s*'{Regex.Escape(projectConfig)}'"))
                    .ToList();

            foreach (var group in itemDefinitionGroups)
            {
                // not using ToDictionary() here since settings may occur multiple times,
                // in this case older definitions should get overwritten by newer definitions
                var projectConfigCompilerSettings = new Dictionary<string, string>();
                foreach (var element in group.Elements(clCompileXName).SelectMany(element => element.Elements()))
                {
                    projectConfigCompilerSettings[element.Name.LocalName] = element.Value;
                }

                var projectConfigLinkerSettings = new Dictionary<string, string>();
                foreach (var element in group.Elements(linkXName).SelectMany(element => element.Elements()))
                {
                    projectConfigLinkerSettings[element.Name.LocalName] = element.Value;
                }

                foreach (var setting in projectConfigCompilerSettings)
                {
                    compilerSettings.TryAdd(setting.Key, []);
                    compilerSettings[setting.Key][projectConfig] = setting.Value;
                }

                foreach (var setting in projectConfigLinkerSettings)
                {
                    linkerSettings.TryAdd(setting.Key, []);
                    linkerSettings[setting.Key][projectConfig] = setting.Value;
                }
            }
        }

        var languageStandard = compilerSettings.GetValueOrDefault("LanguageStandard")?.Values.Distinct().SingleOrDefault();
        if (languageStandard == null)
            Console.WriteLine("Warning: Language standard could not be determined.");

        var includePaths = ConfigDependentSetting.Parse(
            compilerSettings.GetValueOrDefault("AdditionalIncludeDirectories"), ParseIncludePaths);

        var libraries = ConfigDependentSetting.Parse(
            compilerSettings.GetValueOrDefault("AdditionalDependencies"), ParseLibraries);

        var defines = ConfigDependentSetting.Parse(
            compilerSettings.GetValueOrDefault("PreprocessorDefinitions"), ParseDefines);

        var options = ConfigDependentSetting.Parse(
            compilerSettings.GetValueOrDefault("AdditionalOptions"), ParseOptions);

        var conanPackages =
            imports
            .Select(import =>
            {
                var match = Regex.Match(import, @"conan_([A-Za-z0-9-_]+)\.props");
                return match.Success ? match.Groups[1].Value : null;
            })
            .Where(packageName => packageName != null)
            .Select(packageName => conanPackageInfo.GetValueOrDefault(packageName, new ConanPackage(packageName, packageName)))
            .ToArray();

        return new ProjectFileInfo
        {
            AbsoluteProjectPath = Path.GetFullPath(projectPath),
            ProjectName = Path.GetFileNameWithoutExtension(projectPath),
            Languages = DetectLanguages(sourceFiles),
            ConfigurationType = configurationType,
            LanguageStandard = languageStandard,
            SourceFiles = sourceFiles.ToArray(),
            IncludePaths = includePaths,
            Libraries = libraries,
            Defines = defines,
            Options = options,
            ProjectReferences = projectReferences.Select(pr => new ProjectReference { Path = pr }).ToArray(),
            QtModules = qtModules,
            ConanPackages = conanPackages
        };
    }

    static string[] ParseList(string list, char separator, string placeholder)
    {
        return list
            .Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Except([placeholder])
            .Distinct()
            .ToArray();
    }

    static string[] ParseIncludePaths(string includePaths) => ParseList(includePaths, ';', "%(AdditionalIncludeDirectories)");

    static string[] ParseLibraries(string libraries) => ParseList(libraries, ';', "%(AdditionalDependencies)");

    static string[] ParseDefines(string defines) => ParseList(defines, ';', "%(PreprocessorDefinitions)");

    static string[] ParseOptions(string options) => ParseList(options, ' ', "%(AdditionalOptions)");

    static string[] DetectLanguages(IEnumerable<string> sourceFiles)
    {
        List<string> result = new List<string>();

        if (sourceFiles.Any(file => file.EndsWith(".c", StringComparison.OrdinalIgnoreCase)))
            result.Add("C");
        if (sourceFiles.Any(file => Regex.IsMatch(file, @"\.(cpp|cxx|c\+\+|cc|hpp)$", RegexOptions.IgnoreCase)))
            result.Add("CXX");

        if (result.Count == 0)
            Console.WriteLine("Warning: could not detect languages for project");

        return result.ToArray();
    }
}

class ConfigDependentSetting
{
    public required string[] Common { get; init; }
    public required string[] Debug { get; init; }
    public required string[] Release { get; init; }
    public required string[] X86 { get; init; }
    public required string[] X64 { get; init; }

    public static readonly ConfigDependentSetting Empty = new()
    {
        Common = [],
        Debug = [],
        Release = [],
        X86 = [],
        X64 = []
    };

    public static ConfigDependentSetting Parse(Dictionary<string, string>? settings, Func<string, string[]> parser)
    {
        if (settings == null || settings.Count == 0)
            return Empty;

        var parsedSettings = settings.ToDictionary(kvp => kvp.Key, kvp => parser(kvp.Value));

        var allSettingValues = parsedSettings.Values.SelectMany(s => s).Distinct().ToArray();

        var commonSettingValues = allSettingValues.Where(s => parsedSettings.All(kvp => kvp.Value.Contains(s))).ToArray();

        string[] FilterValues(Func<string, bool> selector)
        {
            return allSettingValues
                .Where(s => parsedSettings.All(kvp => selector(kvp.Key) == kvp.Value.Contains(s)))
                .Except(commonSettingValues)
                .ToArray();
        }

        var result = new ConfigDependentSetting
        {
            Common = commonSettingValues,
            Debug = FilterValues(config => config.StartsWith("Debug|")),
            Release = FilterValues(config => config.StartsWith("Release|")),
            X86 = FilterValues(config => config.EndsWith("|Win32") || config.EndsWith("|x86")),
            X64 = FilterValues(config => config.EndsWith("|x64"))
        };

        var skippedSettings = parsedSettings.Values.SelectMany(s => s).Except(result.Common).Except(result.Debug).Except(result.Release).Except(result.X86).Except(result.X64).ToArray();
        if (skippedSettings.Length > 0)
            Console.WriteLine($"Warning: some settings were skipped: {string.Join(", ", skippedSettings)}");

        return result;
    }

    public bool IsEmpty => Common.Length == 0 && Debug.Length == 0 && Release.Length == 0 && X86.Length == 0 && X64.Length == 0;
}

record ConanPackage(string CMakeConfigName, string CMakeTargetName);

class ProjectReference
{
    public required string Path { get; init; }
    public ProjectFileInfo? ProjectFileInfo { get; set; }
}