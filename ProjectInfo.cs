using System.Text.RegularExpressions;
using System.Xml.Linq;

class ProjectInfo
{
    public required string AbsoluteProjectPath { get; init; }
    public required string ProjectName { get; init; }
    public required string[] Languages { get; init; }
    public required string ConfigurationType { get; init; }
    public required string? LanguageStandard { get; init; }
    public required string[] SourceFiles { get; init; }
    public required ConfigDependentSetting IncludePaths { get; init; }
    public required ConfigDependentSetting LinkerPaths { get; init; }
    public required ConfigDependentSetting Libraries { get; init; }
    public required ConfigDependentSetting Defines { get; init; }
    public required ConfigDependentSetting Options { get; init; }
    public required ProjectReference[] ProjectReferences { get; init; }
    public required string[] QtModules { get; init; }
    public required ConanPackage[] ConanPackages { get; init; }

    public static ProjectInfo ParseProjectFile(string projectPath, Dictionary<string, ConanPackage> conanPackageInfo)
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
        var projectElement = doc.Element(projectXName)!;

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

        var linkerPaths = ConfigDependentSetting.Parse(
            linkerSettings.GetValueOrDefault("AdditionalLibraryDirectories"), ParseLinkerPaths);

        var libraries = ConfigDependentSetting.Parse(
            linkerSettings.GetValueOrDefault("AdditionalDependencies"), ParseLibraries);

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
            .Select(packageName => conanPackageInfo.GetValueOrDefault(packageName!, new ConanPackage(packageName!, packageName!)))
            .ToArray();

        return new ProjectInfo
        {
            AbsoluteProjectPath = Path.GetFullPath(projectPath),
            ProjectName = Path.GetFileNameWithoutExtension(projectPath),
            Languages = DetectLanguages(sourceFiles),
            ConfigurationType = configurationType,
            LanguageStandard = languageStandard,
            SourceFiles = sourceFiles.ToArray(),
            IncludePaths = includePaths,
            LinkerPaths = linkerPaths,
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

    static string[] ParseLinkerPaths(string linkerPaths) => ParseList(linkerPaths, ';', "%(AdditionalLibraryDirectories)");

    static string[] ParseLibraries(string libraries) => ParseList(libraries, ';', "%(AdditionalDependencies)");

    static string[] ParseDefines(string defines) => ParseList(defines, ';', "%(PreprocessorDefinitions)");

    static string[] ParseOptions(string options) => ParseList(options, ' ', "%(AdditionalOptions)");

    static string[] DetectLanguages(IEnumerable<string> sourceFiles)
    {
        List<string> result = new();

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
}

class ProjectReference
{
    public required string Path { get; init; }
    public ProjectInfo? ProjectFileInfo { get; set; }
}

record ConanPackage(string CMakeConfigName, string CMakeTargetName);