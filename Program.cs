using Scriban;
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

        var rootCommand = new RootCommand("Converts VC++ projects to CMake");
        rootCommand.AddOption(projectOption);
        rootCommand.AddOption(solutionOption);
        rootCommand.SetHandler(Run, projectOption, solutionOption);

        return rootCommand.Invoke(args);
    }

    static void Run(List<string>? projects, string? solution)
    {
        bool hasProjects = projects != null && projects.Count > 0;
        bool hasSolution = !string.IsNullOrEmpty(solution);
        if (hasProjects == hasSolution)
        {
            Console.Error.WriteLine("Error: Specify either --project or --solution, but not both.");
            Environment.Exit(1);
        }

        var conanPackageInfo = LoadConanPackageInfo();
        var cmakeListsTemplate = LoadCMakeListsTemplate();

        if (hasProjects)
        {
            foreach (var project in projects!)
            {
                ProcessProject(project, cmakeListsTemplate, conanPackageInfo);
            }
        }
        else if (hasSolution)
        {
            var projectPaths = GetProjectsFromSolution(solution!);
            if (projectPaths.Length == 0)
            {
                Console.Error.WriteLine($"No .vcxproj files found in solution: {solution}");
                Environment.Exit(1);
            }
            foreach (var proj in projectPaths)
            {
                string absolutePath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(solution)!, proj));
                ProcessProject(absolutePath, cmakeListsTemplate, conanPackageInfo);
            }
        }
    }

    static void ProcessProject(string projectPath, Template cmakeListsTemplate, Dictionary<string, ConanPackage> conanPackageInfo)
    {
        var projectFileInfo = ProjectFileInfo.ParseProjectFile(projectPath, conanPackageInfo);
        var result = cmakeListsTemplate.Render(projectFileInfo);

        Console.WriteLine($"\n# --- {Path.GetFileName(projectPath)} ---\n");
        Console.WriteLine(result);
    }

    static string[] GetProjectsFromSolution(string solutionPath)
    {
        var projectPaths = new List<string>();
        var regex = new Regex(@"Project\(.*?\) = .*?, ""(.*?\.vcxproj)""", RegexOptions.IgnoreCase);

        foreach (var line in File.ReadLines(solutionPath))
        {
            var match = regex.Match(line);
            if (match.Success)
                projectPaths.Add(match.Groups[1].Value);
        }

        return projectPaths.ToArray();
    }

    static Dictionary<string, ConanPackage> LoadConanPackageInfo()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("vcxproj2cmake.conan-packages.csv");
        using var streamReader = new StreamReader(stream);

        return
            streamReader.ReadToEnd()
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split(','))
            .Select(tokens => (tokens[0], new ConanPackage(tokens[1], tokens[2])))
            .ToDictionary();
    }

    static Template LoadCMakeListsTemplate()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("vcxproj2cmake.CMakeLists.txt.scriban");
        using var streamReader = new StreamReader(stream);
        string content = streamReader.ReadToEnd();
        return Template.Parse(content);
    }
}

class ProjectFileInfo
{
    public required string ProjectName { get; set; }
    public required string ConfigurationType { get; set; }
    public required string? LanguageStandard { get; set; }
    public required string[] SourceFiles { get; set; }
    public required ConfigDependentSetting IncludePaths { get; set; }
    public required ConfigDependentSetting Libraries { get; set; }
    public required ConfigDependentSetting Defines { get; set; }
    public required ConfigDependentSetting Options { get; set; }
    public required string[] QtModules { get; set; }
    public required ConanPackage[] ConanPackages { get; set; }

    public static ProjectFileInfo ParseProjectFile(string project, Dictionary<string, ConanPackage> conanPackageInfo)
    {
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

        var doc = XDocument.Load(project);
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
                    Regex.IsMatch(group.Attribute("Condition")!.Value, $@"'$\(Configuration\)\|$\(Platform\)'\s*==\s*'{projectConfig}'"))
                .ToList();

            foreach (var group in itemDefinitionGroups)
            {
                var projectConfigCompilerSettings =
                    group
                    .Elements(clCompileXName)
                    .SelectMany(element => element.Elements())
                    .ToDictionary(element => element.Name.LocalName, element => element.Value);

                var projectConfigLinkerSettings =
                    group
                    .Elements(linkXName)
                    .SelectMany(element => element.Elements())
                    .ToDictionary(element => element.Name.LocalName, element => element.Value);

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
            ProjectName = Path.GetFileNameWithoutExtension(project),
            ConfigurationType = configurationType,
            LanguageStandard = languageStandard,
            SourceFiles = sourceFiles.ToArray(),
            IncludePaths = includePaths,
            Libraries = libraries,
            Defines = defines,
            Options = options,
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
}

class ConfigDependentSetting
{
    public required string[] Common { get; set; }
    public required string[] Debug { get; set; }
    public required string[] Release { get; set; }
    public required string[] X86 { get; set; }
    public required string[] X64 { get; set; }

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
