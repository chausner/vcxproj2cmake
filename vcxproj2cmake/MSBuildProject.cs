using Microsoft.Extensions.Logging;
using System.Globalization;
using System.IO.Abstractions;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace vcxproj2cmake;

class MSBuildProject
{
    public required string AbsoluteProjectPath { get; init; }
    public required string ProjectName { get; init; }
    public required MSBuildProjectConfig[] ProjectConfigurations { get; init; }
    public required string ConfigurationType { get; init; }
    public required string LanguageStandard { get; init; }
    public required string LanguageStandardC { get; init; }
    public required string[] SourceFiles { get; init; }
    public required string[] HeaderFiles { get; init; }
    public required MSBuildConfigDependentSetting<string> TargetName { get; init; }
    public required MSBuildConfigDependentSetting<string[]> AdditionalIncludeDirectories { get; init; }
    public required MSBuildConfigDependentSetting<string[]> IncludePath { get; init; }
    public required MSBuildConfigDependentSetting<string[]> PublicIncludeDirectories { get; init; }
    public required MSBuildConfigDependentSetting<string[]> AdditionalLibraryDirectories { get; init; }
    public required MSBuildConfigDependentSetting<string[]> LibraryPath { get; init; }
    public required MSBuildConfigDependentSetting<string[]> AdditionalDependencies { get; init; }
    public required MSBuildConfigDependentSetting<string[]> PreprocessorDefinitions { get; init; }
    public required MSBuildConfigDependentSetting<string[]> AdditionalCompileOptions { get; init; }
    public required MSBuildConfigDependentSetting<string[]> AdditionalLinkOptions { get; init; }
    public required MSBuildConfigDependentSetting<string> ModuleDefinitionFile { get; init; }
    public required MSBuildConfigDependentSetting<string> CharacterSet { get; init; }
    public required MSBuildConfigDependentSetting<string> UseOfMfc { get; init; }
    public required MSBuildConfigDependentSetting<string> UseDebugLibraries { get; init; }
    public required MSBuildConfigDependentSetting<string> RuntimeLibrary { get; init; }
    public required MSBuildConfigDependentSetting<string> BasicRuntimeChecks { get; init; }
    public required MSBuildConfigDependentSetting<string[]> DisableSpecificWarnings { get; init; }
    public required MSBuildConfigDependentSetting<string[]> TreatSpecificWarningsAsErrors { get; init; }
    public required MSBuildConfigDependentSetting<string> TreatWarningAsError { get; init; }
    public required MSBuildConfigDependentSetting<string> TreatLinkerWarningAsErrors { get; init; }
    public required MSBuildConfigDependentSetting<string> WarningLevel { get; init; }
    public required MSBuildConfigDependentSetting<string> ExternalWarningLevel { get; init; }
    public required MSBuildConfigDependentSetting<string> TreatAngleIncludeAsExternal { get; init; }
    public required string[] ProjectReferences { get; init; }
    public required string? LinkerSubsystem { get; init; }
    public required bool LinkLibraryDependenciesEnabled { get; init; }
    public required MSBuildConfigDependentSetting<string> PrecompiledHeader { get; init; }
    public required MSBuildConfigDependentSetting<string> PrecompiledHeaderFile { get; init; }
    public required MSBuildConfigDependentSetting<string> AllProjectIncludesArePublic { get; init; }
    public required MSBuildConfigDependentSetting<string> OpenMPSupport { get; init; }
    public required bool RequiresQtMoc { get; init; }
    public required bool RequiresQtUic { get; init; }
    public required bool RequiresQtRcc { get; init; }
    public required string[] QtModules { get; init; }
    public required string[] ConanPackages { get; init; }

    public static MSBuildProject ParseProjectFile(string projectPath, IFileSystem fileSystem, ILogger logger)
    {
        logger.LogInformation($"Parsing {projectPath}");
        projectPath = PathUtils.NormalizePathSeparators(projectPath);
        var absoluteProjectPath = Path.GetFullPath(projectPath);

        XDocument doc;

        using (var fileStream = fileSystem.FileStream.New(projectPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            doc = XDocument.Load(fileStream);

        var projectElement = doc.Elements().Single(element => element.Name.LocalName == "Project");

        var projectConfigurations =
            ElementsNamed(projectElement, "ItemGroup")
                .SelectMany(group => ElementsNamed(group, "ProjectConfiguration"))
                .Select(element => new MSBuildProjectConfig(
                    Name: UnescapeMSBuildValue(element.Attribute("Include")!.Value.Trim()),
                    Configuration: UnescapeMSBuildValue(ElementsNamed(element, "Configuration").Single().Value.Trim()),
                    Platform: UnescapeMSBuildValue(ElementsNamed(element, "Platform").Single().Value.Trim())
                ))
                .ToList();

        var evaluatedProjects =
            projectConfigurations.ToDictionary(
                projectConfig => projectConfig,
                projectConfig => MSBuildProjectEvaluator.Evaluate(projectElement, projectPath, absoluteProjectPath, projectConfig, fileSystem, logger));

        var evaluatedElements =
            evaluatedProjects.Values
                .SelectMany(project => project.Elements)
                .ToList();        

        Dictionary<string, Dictionary<MSBuildProjectConfig, string>> compilerSettings = [];
        Dictionary<string, Dictionary<MSBuildProjectConfig, string>> linkerSettings = [];
        Dictionary<string, Dictionary<MSBuildProjectConfig, string>> otherSettings = [];

        foreach (var projectConfig in projectConfigurations)
        {
            var itemDefinitionGroups =
                evaluatedProjects[projectConfig].Elements
                    .Where(element => element.Name.LocalName == "ItemDefinitionGroup")
                    .Where(group => DoesConfigPlatformConditionApply(group, projectConfig))
                    .ToList();

            var propertyGroups =
                evaluatedProjects[projectConfig].Elements
                    .Where(element => element.Name.LocalName == "PropertyGroup")
                    .Where(group => DoesConfigPlatformConditionApply(group, projectConfig))
                    .ToList();

            var projectConfigCompilerSettings =
                itemDefinitionGroups
                .SelectMany(group => ElementsNamed(group, "ClCompile"))
                .SelectMany(element => element.Elements())
                .ToDictionaryKeepingLast(element => element.Name.LocalName, element => element.Value.Trim());

            var projectConfigLinkerSettings =
                itemDefinitionGroups
                .SelectMany(group => group.Elements())
                .Where(element => element.Name.LocalName is "Link" or "Lib")
                .SelectMany(element => element.Elements())
                .ToDictionaryKeepingLast(element => element.Name.LocalName, element => element.Value.Trim());

            var projectConfigOtherSettings =
                propertyGroups
                .SelectMany(element => element.Elements())
                .ToDictionaryKeepingLast(element => element.Name.LocalName, element => element.Value.Trim());

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

            foreach (var setting in projectConfigOtherSettings)
            {
                otherSettings.TryAdd(setting.Key, []);
                otherSettings[setting.Key][projectConfig] = setting.Value;
            }
        }

        var configurationType = GetCommonSetting("ConfigurationType", otherSettings) ?? "Application";
        var languageStandard = GetCommonSetting("LanguageStandard", compilerSettings) ?? "Default";
        var languageStandardC = GetCommonSetting("LanguageStandard_C", compilerSettings) ?? "Default";
        string? linkerSubsystem = GetCommonSetting("SubSystem", linkerSettings);

        var targetName = ParseSetting("TargetName", otherSettings, Path.GetFileNameWithoutExtension(projectPath));
        var additionalIncludeDirectories = ParseMultiSetting("AdditionalIncludeDirectories", ';', compilerSettings, []);
        var includePath = ParseMultiSetting("IncludePath", ';', otherSettings, []);
        var publicIncludeDirectories = ParseMultiSetting("PublicIncludeDirectories", ';', otherSettings, []);
        var additionalLibraryDirectories = ParseMultiSetting("AdditionalLibraryDirectories", ';', linkerSettings, []);
        var libraryPath = ParseMultiSetting("LibraryPath", ';', otherSettings, []);
        var additionalDependencies = ParseMultiSetting("AdditionalDependencies", ';', linkerSettings, []);
        var preprocessorDefinitions = ParseMultiSetting("PreprocessorDefinitions", ';', compilerSettings, []);
        var additionalCompileOptions = ParseCommandLineOptionSetting(compilerSettings, "AdditionalOptions");
        var additionalLinkOptions = ParseCommandLineOptionSetting(linkerSettings, "AdditionalOptions");
        var moduleDefinitionFile = ParseSetting("ModuleDefinitionFile", linkerSettings, string.Empty);
        var characterSet = ParseSetting("CharacterSet", otherSettings, "NotSet");
        var useOfMfc = ParseSetting("UseOfMfc", otherSettings, "false");
        var useDebugLibraries = ParseSetting("UseDebugLibraries", otherSettings, "false");
        var runtimeLibrary = ParseSettingWithConfigSpecificDefault("RuntimeLibrary", compilerSettings, new(projectConfig =>
        {
            if (useDebugLibraries.GetEffectiveValue(projectConfig).Equals("true", StringComparison.OrdinalIgnoreCase))
                return "MultiThreadedDebugDLL";
            else
                return "MultiThreadedDLL";
        }));
        var basicRuntimeChecks = ParseSettingWithConfigSpecificDefault("BasicRuntimeChecks", compilerSettings, new(projectConfig =>
        {
            if (useDebugLibraries.GetEffectiveValue(projectConfig).Equals("true", StringComparison.OrdinalIgnoreCase))
                return "EnableFastChecks";
            else
                return "Default";
        }));
        var disableSpecificWarnings = ParseMultiSetting("DisableSpecificWarnings", ';', compilerSettings, []);
        var treatSpecificWarningsAsErrors = ParseMultiSetting("TreatSpecificWarningsAsErrors", ';', compilerSettings, []);
        var treatWarningAsError = ParseSetting("TreatWarningAsError", compilerSettings, "false");
        var treatLinkerWarningAsErrors = ParseSetting("TreatLinkerWarningAsErrors", linkerSettings, "false");
        var warningLevel = ParseSetting("WarningLevel", compilerSettings, string.Empty);
        var externalWarningLevel = ParseSetting("ExternalWarningLevel", compilerSettings, string.Empty);
        var treatAngleIncludeAsExternal = ParseSetting("TreatAngleIncludeAsExternal", compilerSettings, "false");
        var allProjectIncludesArePublic = ParseSetting("AllProjectIncludesArePublic", otherSettings, "false");
        var openMPSupport = ParseSetting("OpenMPSupport", compilerSettings, "false");
        var precompiledHeader = ParseSetting("PrecompiledHeader", compilerSettings, "NotUsing");
        var precompiledHeaderFile = ParseSetting("PrecompiledHeaderFile", compilerSettings, string.Empty);

        var sourceFiles =
            evaluatedElements
                .Where(element => element.Name.LocalName == "ItemGroup")
                .SelectMany(group =>
                    ElementsNamed(group, "ClCompile")
                    .Concat(ElementsNamed(group, "ResourceCompile"))
                    .Concat(ElementsNamed(group, "Manifest"))
                    .Concat(ElementsNamed(group, "MASM"))
                    .Concat(ElementsNamed(group, "Natvis"))
                    .Concat(ElementsNamed(group, "QtUic"))
                    .Concat(ElementsNamed(group, "QtRcc")))
                .Select(element => PathUtils.NormalizePathSeparators(UnescapeMSBuildValue(element.Attribute("Include")!.Value.Trim())))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        var headerFiles =
            evaluatedElements
                .Where(element => element.Name.LocalName == "ItemGroup")
                .SelectMany(group => ElementsNamed(group, "ClInclude"))
                .Select(element => PathUtils.NormalizePathSeparators(UnescapeMSBuildValue(element.Attribute("Include")!.Value.Trim())))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        var qtMocHeaderFiles =
            evaluatedElements
                .Where(element => element.Name.LocalName == "ItemGroup")
                .SelectMany(group => ElementsNamed(group, "QtMoc"))
                .Select(element => PathUtils.NormalizePathSeparators(UnescapeMSBuildValue(element.Attribute("Include")!.Value.Trim())))
                .Where(path => Path.GetExtension(path).ToLowerInvariant() is ".h" or ".hpp" or ".hxx" or ".h++" or ".hh")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        var qtModules =
            evaluatedElements
                .Where(element => element.Name.LocalName == "PropertyGroup")
                .SelectMany(group => ElementsNamed(group, "QtModules"))
                .Select(element => element.Value.Trim())
                .Distinct()
                .SingleOrDefaultWithException(string.Empty,
                    () => throw new CatastrophicFailureException("Qt modules are inconsistent between configurations"))
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(UnescapeMSBuildValue)
                .ToArray();

        var imports =
            evaluatedProjects.Values
                .SelectMany(project => project.Imports)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        LogWarningsForUnsupportedImports(imports, logger);
        LogWarningsForUnsupportedFileLevelSettings(evaluatedElements, logger);

        var projectReferences =
            evaluatedElements
                .Where(element => element.Name.LocalName == "ItemGroup")
                .SelectMany(group => ElementsNamed(group, "ProjectReference"))
                .Select(element => PathUtils.NormalizePathSeparators(UnescapeMSBuildValue(element.Attribute("Include")!.Value.Trim())))
                .Distinct()
                .ToList();

        var linkLibraryDependenciesEnabled =
            evaluatedElements
                .Where(element => element.Name.LocalName == "ItemDefinitionGroup")
                .SelectMany(group => ElementsNamed(group, "ProjectReference"))
                .SelectMany(element => ElementsNamed(element, "LinkLibraryDependencies"))
                .Select(element => UnescapeMSBuildValue(element.Value.Trim()))
                .Select(value => value.ToLowerInvariant() switch
                {
                    "true" => true,
                    "false" => false,
                    _ => throw new CatastrophicFailureException(
                        $"Invalid value for LinkLibraryDependencies: {value}")
                })
                .Distinct()
                .SingleOrDefaultWithException(configurationType != "StaticLibrary",
                    () => throw new CatastrophicFailureException(
                        "LinkLibraryDependencies property is inconsistent between configurations"));

        var requiresQtMoc =
            evaluatedElements
            .Where(element => element.Name.LocalName == "ItemGroup")
            .SelectMany(group => ElementsNamed(group, "QtMoc"))
            .Any();

        var requiresQtUic =
            evaluatedElements
            .Where(element => element.Name.LocalName == "ItemGroup")
            .SelectMany(group => ElementsNamed(group, "QtUic"))
            .Any();

        var requiresQtRcc =
            evaluatedElements
            .Where(element => element.Name.LocalName == "ItemGroup")
            .SelectMany(group => ElementsNamed(group, "QtRcc"))
            .Any();
        var conanPackages =
            imports
                .Select(import =>
                {
                    var match = Regex.Match(import, @"conan_([A-Za-z0-9-_]+)\.props", RegexOptions.IgnoreCase);
                    return match.Success ? match.Groups[1].Value : null;
                })
                .Where(packageName => packageName != null)
                .ToArray();

        return new MSBuildProject
        {
            AbsoluteProjectPath = absoluteProjectPath,
            ProjectName = Path.GetFileNameWithoutExtension(projectPath),
            ProjectConfigurations = projectConfigurations.ToArray(),
            ConfigurationType = configurationType,
            LanguageStandard = languageStandard,
            LanguageStandardC = languageStandardC,
            SourceFiles = sourceFiles.ToArray(),
            HeaderFiles = headerFiles.Concat(qtMocHeaderFiles).ToArray(),
            TargetName = targetName,
            AdditionalIncludeDirectories = additionalIncludeDirectories,
            IncludePath = includePath,
            PublicIncludeDirectories = publicIncludeDirectories,
            AdditionalLibraryDirectories = additionalLibraryDirectories,
            LibraryPath = libraryPath,
            AdditionalDependencies = additionalDependencies,
            PreprocessorDefinitions = preprocessorDefinitions,
            AdditionalCompileOptions = additionalCompileOptions,
            AdditionalLinkOptions = additionalLinkOptions,
            ModuleDefinitionFile = moduleDefinitionFile,
            CharacterSet = characterSet,
            UseOfMfc = useOfMfc,
            UseDebugLibraries = useDebugLibraries,
            RuntimeLibrary = runtimeLibrary,
            BasicRuntimeChecks = basicRuntimeChecks,
            DisableSpecificWarnings = disableSpecificWarnings,
            TreatSpecificWarningsAsErrors = treatSpecificWarningsAsErrors,
            TreatWarningAsError = treatWarningAsError,
            TreatLinkerWarningAsErrors = treatLinkerWarningAsErrors,
            WarningLevel = warningLevel,
            ExternalWarningLevel = externalWarningLevel,
            TreatAngleIncludeAsExternal = treatAngleIncludeAsExternal,
            ProjectReferences = projectReferences.ToArray(),
            LinkerSubsystem = linkerSubsystem,
            LinkLibraryDependenciesEnabled = linkLibraryDependenciesEnabled,
            PrecompiledHeader = precompiledHeader,
            PrecompiledHeaderFile = precompiledHeaderFile,
            AllProjectIncludesArePublic = allProjectIncludesArePublic,
            OpenMPSupport = openMPSupport,
            RequiresQtMoc = requiresQtMoc,
            RequiresQtUic = requiresQtUic,
            RequiresQtRcc = requiresQtRcc,
            QtModules = qtModules,
            ConanPackages = conanPackages!
        };

        static string? GetCommonSetting(string property, Dictionary<string, Dictionary<MSBuildProjectConfig, string>> settings)
        {
            return settings.GetValueOrDefault(property)?.Values
                .Select(UnescapeMSBuildValue)
                .Distinct()
                .SingleOrDefaultWithException(null, () => throw new CatastrophicFailureException($"{property} property is inconsistent between configurations"));
        }

        static MSBuildConfigDependentSetting<string> ParseSetting(
            string property,
            Dictionary<string, Dictionary<MSBuildProjectConfig, string>> settings,
            string defaultValue)
        {
            return new(property, defaultValue, settings.GetValueOrDefault(property, []), UnescapeMSBuildValue);
        }

        MSBuildConfigDependentSetting<string> ParseSettingWithConfigSpecificDefault(
            string property,
            Dictionary<string, Dictionary<MSBuildProjectConfig, string>> settings,
            Func<MSBuildProjectConfig, string> defaultValueForConfig)
        {
            var settingsForProperty = settings.GetValueOrDefault(property, []).ToDictionary();

            foreach (var projectConfig in projectConfigurations)
                if (!settingsForProperty.ContainsKey(projectConfig))
                    settingsForProperty[projectConfig] = defaultValueForConfig(projectConfig);

            return new(property, string.Empty, settingsForProperty, UnescapeMSBuildValue);
        }

        static MSBuildConfigDependentSetting<string[]> ParseMultiSetting(
            string property,
            char separator,
            Dictionary<string, Dictionary<MSBuildProjectConfig, string>> settings,
            string[] defaultValue)
        {
            var parser = (string value) =>
                value
                .Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Except([$"%({property})", $"$({property})"], StringComparer.OrdinalIgnoreCase)
                .Select(UnescapeMSBuildValue)
                .Distinct()
                .ToArray();

            return new(property, defaultValue, settings.GetValueOrDefault(property, []), parser);
        }

        static MSBuildConfigDependentSetting<string[]> ParseCommandLineOptionSetting(Dictionary<string, Dictionary<MSBuildProjectConfig, string>> settings, string settingName)
        {
            var parser = (string value) =>
                PathUtils.SplitArguments(value)
                    .Except([$"%({settingName})", $"$({settingName})"], StringComparer.OrdinalIgnoreCase)
                    .Select(UnescapeMSBuildValue)
                    .ToArray();

            return new(settingName, [], settings.GetValueOrDefault(settingName, []), parser);
        }
    }

    static bool DoesConfigPlatformConditionApply(XElement element, MSBuildProjectConfig projectConfig)
    {
        var condition = element.Attribute("Condition")?.Value;
        return condition == null ||
            !Regex.IsMatch(condition, @"'\$\(Configuration\)\|\$\(Platform\)'\s*==\s*'[^']+'") ||
            Regex.IsMatch(condition, $@"'\$\(Configuration\)\|\$\(Platform\)'\s*==\s*'{Regex.Escape(projectConfig.Name)}'");
    }

    static string UnescapeMSBuildValue(string value)
    {
        if (string.IsNullOrEmpty(value) || !value.Contains('%'))
            return value;

        var builder = new StringBuilder(value.Length);

        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '%' &&
                i + 2 < value.Length &&
                byte.TryParse(value.AsSpan(i + 1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var escapedByte))
            {
                builder.Append((char)escapedByte);
                i += 2;
                continue;
            }

            builder.Append(value[i]);
        }

        return builder.ToString();
    }

    static readonly string[] StandardVisualStudioImports =
    [
        PathUtils.NormalizePathSeparators(@"$(VCTargetsPath)\Microsoft.Cpp.Default.props"),
        PathUtils.NormalizePathSeparators(@"$(VCTargetsPath)\Microsoft.Cpp.props"),
        PathUtils.NormalizePathSeparators(@"$(VCTargetsPath)\Microsoft.Cpp.targets"),
        PathUtils.NormalizePathSeparators(@"$(UserRootDir)\Microsoft.Cpp.$(Platform).user.props")
    ];

    static readonly Regex QtMsBuildImportPattern = new(@"^\$\(QtMsBuild\)[\\/]", RegexOptions.IgnoreCase);

    static readonly Regex ConanImportPattern = new(@"conan_[A-Za-z0-9-_.]+\.props|conandeps\.props$", RegexOptions.IgnoreCase);

    static readonly HashSet<string> FileItemElementNames = new(StringComparer.Ordinal)
    {
        "ClCompile",
        "ClInclude",
        "Manifest",
        "MASM",
        "Natvis",
        "QtMoc",
        "QtRcc",
        "QtUic",
        "ResourceCompile"
    };

    static IEnumerable<XElement> ElementsNamed(XElement element, string localName)
        => element.Elements().Where(child => child.Name.LocalName == localName);

    static void LogWarningsForUnsupportedImports(IEnumerable<string> imports, ILogger logger)
    {
        var unsupportedImports =
            imports
            .Where(import => !IsKnownImport(import))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var import in unsupportedImports)
            logger.LogWarning($"MSBuild imports are unsupported and will not be processed: {import}");

        bool IsKnownImport(string import)
        {
            var normalizedImport = PathUtils.NormalizePathSeparators(import);

            return StandardVisualStudioImports.Contains(normalizedImport, StringComparer.OrdinalIgnoreCase) ||
                QtMsBuildImportPattern.IsMatch(normalizedImport) ||
                ConanImportPattern.IsMatch(normalizedImport);
        }
    }

    static void LogWarningsForUnsupportedFileLevelSettings(IEnumerable<XElement> evaluatedElements, ILogger logger)
    {
        var fileLevelSettings =
            evaluatedElements
                .Where(element => element.Name.LocalName == "ItemGroup")
                .SelectMany(group => group.Elements())
                .Where(element => FileItemElementNames.Contains(element.Name.LocalName))
                .Select(element => new
                {
                    FilePath = element.Attribute("Include")?.Value,
                    SettingNames =
                        element.Elements()
                            .Select(setting => setting.Name.LocalName)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToArray()
                })
                .Where(item => !string.IsNullOrWhiteSpace(item.FilePath) && item.SettingNames.Length > 0)
                .GroupBy(
                    item => PathUtils.NormalizePathSeparators(UnescapeMSBuildValue(item.FilePath!.Trim())),
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => new
                {
                    FilePath = group.Key,
                    SettingNames =
                        group.SelectMany(item => item.SettingNames)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .Order(StringComparer.OrdinalIgnoreCase)
                            .ToArray()
                });

        foreach (var fileLevelSetting in fileLevelSettings)
            logger.LogWarning(
                $"File-level MSBuild settings are unsupported and will not be processed: {fileLevelSetting.FilePath} ({string.Join(", ", fileLevelSetting.SettingNames)})");
    }

    sealed class MSBuildProjectEvaluator
    {
        static readonly Regex PropertyReferencePattern = new(@"\$\(([A-Za-z0-9_.-]+)\)", RegexOptions.Compiled);
        const string GetPathOfFileAbovePrefix = "$([MSBuild]::GetPathOfFileAbove(";

        readonly string absoluteProjectPath;
        readonly MSBuildProjectConfig projectConfig;
        readonly IFileSystem fileSystem;
        readonly ILogger logger;
        readonly Dictionary<string, string> properties = new(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> importedStandardDirectoryBuildFiles = new(StringComparer.OrdinalIgnoreCase);
        readonly string projectDirectory;

        public List<XElement> Elements { get; } = [];
        public List<string> Imports { get; } = [];

        MSBuildProjectEvaluator(
            string projectPath,
            string absoluteProjectPath,
            MSBuildProjectConfig projectConfig,
            IFileSystem fileSystem,
            ILogger logger)
        {
            this.absoluteProjectPath = absoluteProjectPath;
            this.projectConfig = projectConfig;
            this.fileSystem = fileSystem;
            this.logger = logger;

            projectDirectory = fileSystem.Path.GetDirectoryName(absoluteProjectPath) ?? string.Empty;

            var configAndPlatform = projectConfig.Name.Split('|', 2);

            properties["Configuration"] = configAndPlatform[0];
            if (configAndPlatform.Length > 1)
                properties["Platform"] = configAndPlatform[1];

            properties["MSBuildProjectFullPath"] = absoluteProjectPath;
            properties["MSBuildProjectDirectory"] = projectDirectory;
            properties["MSBuildProjectDirectoryNoRoot"] = projectDirectory;
            properties["ProjectDir"] = EnsureTrailingSeparator(projectDirectory);
            properties["ProjectPath"] = projectPath;
        }

        public static EvaluatedProject Evaluate(
            XElement projectElement,
            string projectPath,
            string absoluteProjectPath,
            MSBuildProjectConfig projectConfig,
            IFileSystem fileSystem,
            ILogger logger)
        {
            var evaluator = new MSBuildProjectEvaluator(projectPath, absoluteProjectPath, projectConfig, fileSystem, logger);
            evaluator.EvaluateProjectElement(projectElement, absoluteProjectPath, []);
            return new(evaluator.Elements, evaluator.Imports);
        }

        void EvaluateProjectElement(XElement projectElement, string currentFilePath, HashSet<string> importStack)
        {
            foreach (var child in projectElement.Elements())
            {
                if (!DoesConditionApply(child, currentFilePath))
                    continue;

                switch (child.Name.LocalName)
                {
                    case "Import":
                        EvaluateImport(child, currentFilePath, importStack);
                        break;

                    case "ImportGroup":
                        EvaluateImportGroup(child, currentFilePath, importStack);
                        break;

                    case "PropertyGroup":
                        Elements.Add(child);
                        ApplyProperties(child, currentFilePath);
                        break;

                    default:
                        Elements.Add(child);
                        break;
                }
            }
        }

        void EvaluateImportGroup(XElement importGroup, string currentFilePath, HashSet<string> importStack)
        {
            foreach (var import in ElementsNamed(importGroup, "Import"))
            {
                if (DoesConditionApply(import, currentFilePath))
                    EvaluateImport(import, currentFilePath, importStack);
            }
        }

        void EvaluateImport(XElement import, string currentFilePath, HashSet<string> importStack)
        {
            var importProject = import.Attribute("Project")?.Value.Trim();
            if (string.IsNullOrWhiteSpace(importProject))
                return;

            importProject = UnescapeMSBuildValue(importProject);
            Imports.Add(PathUtils.NormalizePathSeparators(importProject));

            var evaluatedImportProject = ExpandValue(importProject, currentFilePath);

            if (IsStandardCppPropsImport(evaluatedImportProject))
            {
                EvaluateStandardDirectoryBuildFile("Directory.Build.props", importStack);
                return;
            }

            if (IsStandardCppTargetsImport(evaluatedImportProject))
            {
                EvaluateStandardDirectoryBuildFile("Directory.Build.targets", importStack);
                return;
            }

            var importedFilePath = ResolvePath(evaluatedImportProject, currentFilePath);
            if (importedFilePath == null || !fileSystem.File.Exists(importedFilePath))
            {
                LogWarningsForUnsupportedImports([importProject], logger);
                return;
            }

            EvaluateImportedFile(importedFilePath, importStack);
        }

        void EvaluateStandardDirectoryBuildFile(string fileName, HashSet<string> importStack)
        {
            var directoryBuildFile = GetPathOfFileAbove(fileName, projectDirectory, absoluteProjectPath);
            if (string.IsNullOrEmpty(directoryBuildFile))
                return;

            if (!importedStandardDirectoryBuildFiles.Add(directoryBuildFile))
                return;

            EvaluateImportedFile(directoryBuildFile, importStack);
        }

        void EvaluateImportedFile(string importedFilePath, HashSet<string> importStack)
        {
            importedFilePath = fileSystem.Path.GetFullPath(importedFilePath);
            if (!importStack.Add(importedFilePath))
                return;

            XDocument importedDocument;
            using (var fileStream = fileSystem.FileStream.New(importedFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                importedDocument = XDocument.Load(fileStream);

            var importedProjectElement = importedDocument.Elements().SingleOrDefault(element => element.Name.LocalName == "Project");
            if (importedProjectElement != null)
                EvaluateProjectElement(importedProjectElement, importedFilePath, importStack);

            importStack.Remove(importedFilePath);
        }

        void ApplyProperties(XElement propertyGroup, string currentFilePath)
        {
            foreach (var property in propertyGroup.Elements())
            {
                if (!DoesConditionApply(property, currentFilePath))
                    continue;

                properties[property.Name.LocalName] = ExpandValue(property.Value.Trim(), currentFilePath);
            }
        }

        bool DoesConditionApply(XElement element, string currentFilePath)
        {
            var condition = element.Attribute("Condition")?.Value;
            if (string.IsNullOrWhiteSpace(condition))
                return true;

            if (!DoesConfigPlatformConditionApply(element, projectConfig))
                return false;

            return EvaluateCondition(condition, currentFilePath);
        }

        bool EvaluateCondition(string condition, string currentFilePath)
        {
            condition = condition.Trim();

            var andParts = Regex.Split(condition, @"\s+and\s+", RegexOptions.IgnoreCase);
            if (andParts.Length > 1)
                return andParts.All(part => EvaluateCondition(part, currentFilePath));

            var orParts = Regex.Split(condition, @"\s+or\s+", RegexOptions.IgnoreCase);
            if (orParts.Length > 1)
                return orParts.Any(part => EvaluateCondition(part, currentFilePath));

            if (Regex.IsMatch(condition, @"'\$\(Configuration\)\|\$\(Platform\)'\s*==\s*'[^']+'", RegexOptions.IgnoreCase))
                return true;

            var existsMatch = Regex.Match(condition, @"^exists\s*\((.*)\)$", RegexOptions.IgnoreCase);
            if (existsMatch.Success)
                return FileExists(EvaluateConditionOperand(existsMatch.Groups[1].Value, currentFilePath), currentFilePath);

            var comparisonMatch = Regex.Match(condition, @"^(.*?)\s*(==|!=)\s*(.*?)$");
            if (comparisonMatch.Success)
            {
                var left = EvaluateConditionOperand(comparisonMatch.Groups[1].Value, currentFilePath);
                var right = EvaluateConditionOperand(comparisonMatch.Groups[3].Value, currentFilePath);
                var equals = string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
                return comparisonMatch.Groups[2].Value == "==" ? equals : !equals;
            }

            return true;
        }

        string EvaluateConditionOperand(string operand, string currentFilePath)
        {
            operand = operand.Trim();
            if ((operand.StartsWith('\'') && operand.EndsWith('\'')) ||
                (operand.StartsWith('"') && operand.EndsWith('"')))
            {
                operand = operand[1..^1];
            }

            return ExpandValue(operand, currentFilePath);
        }

        bool FileExists(string path, string currentFilePath)
        {
            var resolvedPath = ResolvePath(path, currentFilePath);
            return resolvedPath != null && fileSystem.File.Exists(resolvedPath);
        }

        string ExpandValue(string value, string currentFilePath)
        {
            value = ExpandMSBuildThisFileProperties(value, currentFilePath);
            value = ExpandGetPathOfFileAbove(value, currentFilePath);
            return PropertyReferencePattern.Replace(value, match =>
            {
                var propertyName = match.Groups[1].Value;
                return properties.GetValueOrDefault(propertyName, match.Value);
            });
        }

        string ExpandMSBuildThisFileProperties(string value, string currentFilePath)
        {
            var currentFileDirectory = fileSystem.Path.GetDirectoryName(currentFilePath) ?? string.Empty;
            return value
                .Replace("$(MSBuildThisFileFullPath)", currentFilePath, StringComparison.OrdinalIgnoreCase)
                .Replace("$(MSBuildThisFileDirectory)", EnsureTrailingSeparator(currentFileDirectory), StringComparison.OrdinalIgnoreCase)
                .Replace("$(MSBuildThisFile)", fileSystem.Path.GetFileName(currentFilePath), StringComparison.OrdinalIgnoreCase)
                .Replace("$(MSBuildThisFileName)", fileSystem.Path.GetFileNameWithoutExtension(currentFilePath), StringComparison.OrdinalIgnoreCase)
                .Replace("$(MSBuildThisFileExtension)", fileSystem.Path.GetExtension(currentFilePath), StringComparison.OrdinalIgnoreCase);
        }

        string ExpandGetPathOfFileAbove(string value, string currentFilePath)
        {
            var index = value.IndexOf(GetPathOfFileAbovePrefix, StringComparison.OrdinalIgnoreCase);
            while (index >= 0)
            {
                var functionEnd = FindGetPathOfFileAboveEnd(value, index);
                if (functionEnd == null)
                    break;

                var argumentsStart = index + GetPathOfFileAbovePrefix.Length;
                var arguments = value[argumentsStart..functionEnd.Value];
                var replacement = EvaluateGetPathOfFileAbove(arguments, currentFilePath);
                value = value[..index] + replacement + value[Math.Min(functionEnd.Value + 2, value.Length)..];

                index = value.IndexOf(GetPathOfFileAbovePrefix, index + replacement.Length, StringComparison.OrdinalIgnoreCase);
            }

            return value;
        }

        int? FindGetPathOfFileAboveEnd(string value, int functionStart)
        {
            var index = functionStart + GetPathOfFileAbovePrefix.Length;
            var depth = 1;
            var quote = '\0';

            for (; index < value.Length; index++)
            {
                var current = value[index];
                if (quote != '\0')
                {
                    if (current == quote)
                        quote = '\0';
                    continue;
                }

                if (current is '\'' or '"')
                {
                    quote = current;
                    continue;
                }

                if (current == '$' && index + 1 < value.Length && value[index + 1] == '(')
                {
                    index = value.IndexOf(')', index + 2);
                    if (index < 0)
                        return null;

                    continue;
                }

                if (current == '(')
                    depth++;
                else if (current == ')' && --depth == 0)
                    return index;
            }

            return null;
        }

        string EvaluateGetPathOfFileAbove(string arguments, string currentFilePath)
        {
            var args = SplitArguments(arguments)
                .Select(argument => ExpandValue(StripQuotes(argument.Trim()), currentFilePath))
                .ToArray();

            if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
                return string.Empty;

            var startDirectory = args.Length > 1 && !string.IsNullOrWhiteSpace(args[1])
                ? args[1]
                : fileSystem.Path.GetDirectoryName(currentFilePath) ?? string.Empty;

            return GetPathOfFileAbove(args[0], startDirectory, currentFilePath);
        }

        string GetPathOfFileAbove(string fileName, string startDirectory, string currentFilePath)
        {
            var currentDirectory = ResolveDirectory(startDirectory, currentFilePath);
            if (currentDirectory == null)
                return string.Empty;

            while (!string.IsNullOrEmpty(currentDirectory))
            {
                var candidate = fileSystem.Path.GetFullPath(fileSystem.Path.Combine(currentDirectory, fileName));
                if (fileSystem.File.Exists(candidate))
                    return candidate;

                var parent = fileSystem.Path.GetDirectoryName(currentDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (string.IsNullOrEmpty(parent) || string.Equals(parent, currentDirectory, StringComparison.OrdinalIgnoreCase))
                    break;

                currentDirectory = parent;
            }

            return string.Empty;
        }

        string? ResolvePath(string path, string currentFilePath)
        {
            path = StripQuotes(path.Trim());
            if (string.IsNullOrWhiteSpace(path) || path.Contains("$("))
                return null;

            path = PathUtils.NormalizePathSeparators(path);
            if (fileSystem.Path.IsPathRooted(path))
                return fileSystem.Path.GetFullPath(path);

            var currentDirectory = fileSystem.Path.GetDirectoryName(currentFilePath);
            if (string.IsNullOrEmpty(currentDirectory))
                return null;

            return fileSystem.Path.GetFullPath(fileSystem.Path.Combine(currentDirectory, path));
        }

        string? ResolveDirectory(string directory, string currentFilePath)
        {
            var resolvedPath = ResolvePath(directory, currentFilePath);
            if (resolvedPath == null)
                return null;

            return fileSystem.Path.GetFullPath(resolvedPath);
        }

        static string[] SplitArguments(string arguments)
        {
            List<string> result = [];
            var start = 0;
            var quote = '\0';

            for (var index = 0; index < arguments.Length; index++)
            {
                var current = arguments[index];
                if (quote != '\0')
                {
                    if (current == quote)
                        quote = '\0';
                    continue;
                }

                if (current is '\'' or '"')
                {
                    quote = current;
                    continue;
                }

                if (current == ',')
                {
                    result.Add(arguments[start..index]);
                    start = index + 1;
                }
            }

            result.Add(arguments[start..]);
            return result.ToArray();
        }

        static string StripQuotes(string value)
        {
            value = value.Trim();
            if ((value.StartsWith('\'') && value.EndsWith('\'')) ||
                (value.StartsWith('"') && value.EndsWith('"')))
            {
                return value[1..^1];
            }

            return value;
        }

        static bool IsStandardCppPropsImport(string importProject)
        {
            var fileName = Path.GetFileName(PathUtils.NormalizePathSeparators(importProject));
            return string.Equals(fileName, "Microsoft.Cpp.props", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "Microsoft.Cpp.Default.props", StringComparison.OrdinalIgnoreCase);
        }

        static bool IsStandardCppTargetsImport(string importProject)
        {
            var fileName = Path.GetFileName(PathUtils.NormalizePathSeparators(importProject));
            return string.Equals(fileName, "Microsoft.Cpp.targets", StringComparison.OrdinalIgnoreCase);
        }

        static string EnsureTrailingSeparator(string directory)
        {
            if (string.IsNullOrEmpty(directory) ||
                directory.EndsWith(Path.DirectorySeparatorChar) ||
                directory.EndsWith(Path.AltDirectorySeparatorChar))
            {
                return directory;
            }

            return directory + Path.DirectorySeparatorChar;
        }
    }

    sealed record EvaluatedProject(List<XElement> Elements, List<string> Imports);
}
