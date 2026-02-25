using Microsoft.Extensions.Logging;
using Microsoft.Build.Construction;
using System.IO.Abstractions;
using System.Text.RegularExpressions;
using System.Xml;

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
    public required MSBuildConfigDependentSetting<string[]> AdditionalOptions { get; init; }
    public required MSBuildConfigDependentSetting<string> CharacterSet { get; init; }
    public required MSBuildConfigDependentSetting<string> RuntimeLibrary { get; init; }
    public required MSBuildConfigDependentSetting<string[]> DisableSpecificWarnings { get; init; }
    public required MSBuildConfigDependentSetting<string[]> TreatSpecificWarningsAsErrors { get; init; }
    public required MSBuildConfigDependentSetting<string> TreatWarningAsError { get; init; }
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

        ProjectRootElement projectElement;

        using (var stream = fileSystem.FileStream.New(projectPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var reader = XmlReader.Create(stream))
            projectElement = ProjectRootElement.Create(reader);

        projectElement.FullPath = projectPath;

        var projectConfigurations =
            projectElement.ItemGroups
                .SelectMany(g => g.Items.Where(i => i.ItemType == "ProjectConfiguration"))
                .Select(i => PathUtils.NormalizePathSeparators(i.Include.Trim()))
                .Select(config => new MSBuildProjectConfig(config))
                .ToList();

        var sourceFiles =
            projectElement.ItemGroups
                .SelectMany(g => g.Items.Where(i => i.ItemType == "ClCompile" || i.ItemType == "QtUic" || i.ItemType == "QtRcc"))
                .Select(i => PathUtils.NormalizePathSeparators(i.Include.Trim()))
                .ToList();

        var headerFiles =
            projectElement.ItemGroups
                .SelectMany(g => g.Items.Where(i => i.ItemType == "ClInclude"))
                .Select(i => PathUtils.NormalizePathSeparators(i.Include.Trim()))
                .ToList();

        var qtMocHeaderFiles =
            projectElement
                .Elements(itemGroupXName)
                .SelectMany(group => group.Elements(qtMocXName))
                .Select(element => PathUtils.NormalizePathSeparators(element.Attribute("Include")!.Value.Trim()))
                .Where(path => Path.GetExtension(path).ToLowerInvariant() is ".h" or ".hpp" or ".hxx" or ".h++" or ".hh")
                .ToList();

        var qtModules =
            projectElement.PropertyGroups
                .SelectMany(g => g.Properties.Where(p => p.Name == "QtModules"))
                .Select(p => p.Value.Trim())
                .Distinct()
                .SingleOrDefaultWithException(string.Empty,
                    () => throw new CatastrophicFailureException("Qt modules are inconsistent between configurations"))
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var imports =
            projectElement.Imports.Select(i => PathUtils.NormalizePathSeparators(i.Project.Trim()))
                .Concat(projectElement.ImportGroups.SelectMany(g => g.Imports.Select(i => PathUtils.NormalizePathSeparators(i.Project.Trim()))))
                .ToList();

        var projectReferences =
            projectElement.ItemGroups
                .SelectMany(g => g.Items.Where(i => i.ItemType == "ProjectReference"))
                .Select(i => PathUtils.NormalizePathSeparators(i.Include.Trim()))
                .Distinct()
                .ToList();

        var linkLibraryDependenciesEnabled =
            projectElement.ItemDefinitionGroups
                .SelectMany(g => g.ItemDefinitions.Where(d => d.ItemType == "ProjectReference"))
                .SelectMany(d => d.Metadata.Where(m => m.Name == "LinkLibraryDependencies"))
                .Select(m => m.Value.Trim() switch
                {
                    "true" => true,
                    "false" => false,
                    _ => throw new CatastrophicFailureException(
                        $"Invalid value for LinkLibraryDependencies: {m.Value}")
                })
                .Distinct()
                .SingleOrDefaultWithException(true,
                    () => throw new CatastrophicFailureException(
                        "LinkLibraryDependencies property is inconsistent between configurations"));

        var requiresQtMoc =
            projectElement.ItemGroups
            .SelectMany(g => g.Items)
            .Any(i => i.ItemType == "QtMoc");

        var requiresQtUic =
            projectElement.ItemGroups
            .SelectMany(g => g.Items)
            .Any(i => i.ItemType == "QtUic");

        var requiresQtRcc =
            projectElement.ItemGroups
            .SelectMany(g => g.Items)
            .Any(i => i.ItemType == "QtRcc");

        Dictionary<string, Dictionary<MSBuildProjectConfig, string>> compilerSettings = [];
        Dictionary<string, Dictionary<MSBuildProjectConfig, string>> linkerSettings = [];
        Dictionary<string, Dictionary<MSBuildProjectConfig, string>> otherSettings = [];

        foreach (var projectConfig in projectConfigurations)
        {
            var itemDefinitionGroups =
                projectElement.ItemDefinitionGroups
                    .Where(group => string.IsNullOrEmpty(group.Condition) ||
                                    Regex.IsMatch(group.Condition!,
                                        $@"'\$\(Configuration\)\|\$\(Platform\)'\s*==\s*'{Regex.Escape(projectConfig.Name)}'"))
                    .ToList();

            var propertyGroups =
                projectElement.PropertyGroups
                    .Where(group => string.IsNullOrEmpty(group.Condition) ||
                                    Regex.IsMatch(group.Condition!,
                                        $@"'\$\(Configuration\)\|\$\(Platform\)'\s*==\s*'{Regex.Escape(projectConfig.Name)}'"))
                    .ToList();

            var projectConfigCompilerSettings =
                itemDefinitionGroups
                .SelectMany(group => group.ItemDefinitions.Where(d => d.ItemType == "ClCompile"))
                .SelectMany(element => element.Metadata)
                .ToDictionaryKeepingLast(element => element.Name, element => element.Value.Trim());

            var projectConfigLinkerSettings =
                itemDefinitionGroups
                .SelectMany(group => group.ItemDefinitions.Where(d => d.ItemType == "Link" || d.ItemType == "Lib"))
                .SelectMany(element => element.Metadata)
                .ToDictionaryKeepingLast(element => element.Name, element => element.Value.Trim());

            var projectConfigOtherSettings =
                propertyGroups
                .SelectMany(group => group.Properties)
                .ToDictionaryKeepingLast(prop => prop.Name, prop => prop.Value.Trim());

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

        var targetName = ParseSetting("TargetName", otherSettings, Path.GetFileNameWithoutExtension(projectPath));
        var additionalIncludeDirectories = ParseMultiSetting("AdditionalIncludeDirectories", ';', compilerSettings, []);
        var includePath = ParseMultiSetting("IncludePath", ';', otherSettings, []);
        var publicIncludeDirectories = ParseMultiSetting("PublicIncludeDirectories", ';', otherSettings, []);
        var additionalLibraryDirectories = ParseMultiSetting("AdditionalLibraryDirectories", ';', linkerSettings, []);
        var libraryPath = ParseMultiSetting("LibraryPath", ';', otherSettings, []);
        var additionalDependencies = ParseMultiSetting("AdditionalDependencies", ';', linkerSettings, []);
        var preprocessorDefinitions = ParseMultiSetting("PreprocessorDefinitions", ';', compilerSettings, []);
        var additionalOptions = ParseMultiSetting("AdditionalOptions", ' ', compilerSettings, []);
        var characterSet = ParseSetting("CharacterSet", otherSettings, "NotSet");
        var useDebugLibraries = ParseSetting("UseDebugLibraries", otherSettings, "false");
        var runtimeLibrary = ParseSettingWithConfigSpecificDefault("RuntimeLibrary", compilerSettings, new(projectConfig => {
            if (useDebugLibraries.GetEffectiveValue(projectConfig) == "true")
                return "MultiThreadedDebugDLL";
            else
                return "MultiThreadedDLL";
        }));
        var disableSpecificWarnings = ParseMultiSetting("DisableSpecificWarnings", ';', compilerSettings, []);
        var treatSpecificWarningsAsErrors = ParseMultiSetting("TreatSpecificWarningsAsErrors", ';', compilerSettings, []);
        var treatWarningAsError = ParseSetting("TreatWarningAsError", compilerSettings, "false");
        var warningLevel = ParseSetting("WarningLevel", compilerSettings, string.Empty);
        var externalWarningLevel = ParseSetting("ExternalWarningLevel", compilerSettings, string.Empty);
        var treatAngleIncludeAsExternal = ParseSetting("TreatAngleIncludeAsExternal", compilerSettings, "false");
        var allProjectIncludesArePublic = ParseSetting("AllProjectIncludesArePublic", otherSettings, "false");
        var openMPSupport = ParseSetting("OpenMPSupport", compilerSettings, "false");
        var precompiledHeader = ParseSetting("PrecompiledHeader", compilerSettings, "NotUsing");
        var precompiledHeaderFile = ParseSetting("PrecompiledHeaderFile", compilerSettings, string.Empty);

        var conanPackages =
            imports
                .Select(import =>
                {
                    var match = Regex.Match(import, @"conan_([A-Za-z0-9-_]+)\.props", RegexOptions.IgnoreCase);
                    return match.Success ? match.Groups[1].Value : null;
                })
                .Where(packageName => packageName != null)
                .ToArray();

        string? linkerSubsystem = GetCommonSetting("SubSystem", linkerSettings);

        return new MSBuildProject
        {
            AbsoluteProjectPath = Path.GetFullPath(projectPath),
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
            AdditionalOptions = additionalOptions,
            CharacterSet = characterSet,
            RuntimeLibrary = runtimeLibrary,
            DisableSpecificWarnings = disableSpecificWarnings,
            TreatSpecificWarningsAsErrors = treatSpecificWarningsAsErrors,
            TreatWarningAsError = treatWarningAsError,
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

        string? GetCommonSetting(string property, Dictionary<string, Dictionary<MSBuildProjectConfig, string>> settings)
        {
            return settings.GetValueOrDefault(property)?.Values
                .Distinct()
                .SingleOrDefaultWithException(null, () => throw new CatastrophicFailureException($"{property} property is inconsistent between configurations"));
        }

        MSBuildConfigDependentSetting<string> ParseSetting(
            string property,
            Dictionary<string, Dictionary<MSBuildProjectConfig, string>> settings,
            string defaultValue)
        {
            return new(property, defaultValue, settings.GetValueOrDefault(property, []), value => value);
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

            return new(property, string.Empty, settingsForProperty, value => value);
        }

        MSBuildConfigDependentSetting<string[]> ParseMultiSetting(
            string property,
            char separator,
            Dictionary<string, Dictionary<MSBuildProjectConfig, string>> settings,
            string[] defaultValue)
        {
            var parser = (string value) =>
                value
                .Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Except([$"%({property})", $"$({property})"], StringComparer.OrdinalIgnoreCase)
                .Distinct()
                .ToArray();

            return new(property, defaultValue, settings.GetValueOrDefault(property, []), parser);
        }

    }
}
