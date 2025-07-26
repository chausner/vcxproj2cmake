using Microsoft.Extensions.Logging;
using System.IO.Abstractions;
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
    public required MSBuildConfigDependentSetting<string[]> PublicIncludeDirectories { get; init; }
    public required MSBuildConfigDependentSetting<string[]> AdditionalLibraryDirectories { get; init; }
    public required MSBuildConfigDependentSetting<string[]> AdditionalDependencies { get; init; }
    public required MSBuildConfigDependentSetting<string[]> PreprocessorDefinitions { get; init; }
    public required MSBuildConfigDependentSetting<string[]> AdditionalOptions { get; init; }
    public required MSBuildConfigDependentSetting<string> CharacterSet { get; init; }
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

        var msbuildNamespace = "http://schemas.microsoft.com/developer/msbuild/2003";
        var clCompileXName = XName.Get("ClCompile", msbuildNamespace);
        var clIncludeXName = XName.Get("ClInclude", msbuildNamespace);
        var importGroupXName = XName.Get("ImportGroup", msbuildNamespace);
        var importXName = XName.Get("Import", msbuildNamespace);
        var itemDefinitionGroupXName = XName.Get("ItemDefinitionGroup", msbuildNamespace);
        var itemGroupXName = XName.Get("ItemGroup", msbuildNamespace);
        var libXName = XName.Get("Lib", msbuildNamespace);
        var linkLibraryDependenciesXName = XName.Get("LinkLibraryDependencies", msbuildNamespace);
        var linkXName = XName.Get("Link", msbuildNamespace);
        var projectConfigurationXName = XName.Get("ProjectConfiguration", msbuildNamespace);
        var projectReferenceXName = XName.Get("ProjectReference", msbuildNamespace);
        var projectXName = XName.Get("Project", msbuildNamespace);
        var propertyGroupXName = XName.Get("PropertyGroup", msbuildNamespace);
        var qtMocXName = XName.Get("QtMoc", msbuildNamespace);
        var qtModulesXName = XName.Get("QtModules", msbuildNamespace);
        var qtRccXName = XName.Get("QtRcc", msbuildNamespace);
        var qtUicXName = XName.Get("QtUic", msbuildNamespace);

        XDocument doc;
        projectPath = PathUtils.NormalizePathSeparators(projectPath);

        using (var fileStream = fileSystem.FileStream.New(projectPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            doc = XDocument.Load(fileStream);

        var projectElement = doc.Element(projectXName)!;

        var projectConfigurations =
            projectElement
                .Elements(itemGroupXName)
                .SelectMany(group => group.Elements(projectConfigurationXName))
                .Select(element => PathUtils.NormalizePathSeparators(element.Attribute("Include")!.Value.Trim()))
                .Select(config => new MSBuildProjectConfig(config))
                .ToList();

        var sourceFiles =
            projectElement
                .Elements(itemGroupXName)
                .SelectMany(group =>
                    group.Elements(clCompileXName)
                    .Concat(group.Elements(qtUicXName))
                    .Concat(group.Elements(qtRccXName)))
                .Select(element => PathUtils.NormalizePathSeparators(element.Attribute("Include")!.Value.Trim()))
                .ToList();

        var headerFiles =
            projectElement
                .Elements(itemGroupXName)
                .SelectMany(group => group.Elements(clIncludeXName))
                .Select(element => PathUtils.NormalizePathSeparators(element.Attribute("Include")!.Value.Trim()))
                .ToList();

        var qtModules =
            projectElement
                .Elements(propertyGroupXName)
                .SelectMany(group => group.Elements(qtModulesXName))
                .Select(element => element.Value.Trim())
                .Distinct()
                .SingleOrDefaultWithException(string.Empty,
                    () => throw new CatastrophicFailureException("Qt modules are inconsistent between configurations"))
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var imports =
            projectElement
                .Elements(importXName)
                .Concat(projectElement.Elements(importGroupXName).SelectMany(group => group.Elements(importXName)))
                .Select(import => PathUtils.NormalizePathSeparators(import.Attribute("Project")!.Value.Trim()))
                .ToList();

        var projectReferences =
            projectElement
                .Elements(itemGroupXName)
                .SelectMany(group => group.Elements(projectReferenceXName))
                .Select(element => PathUtils.NormalizePathSeparators(element.Attribute("Include")!.Value.Trim()))
                .Distinct()
                .ToList();

        var linkLibraryDependenciesEnabled =
            projectElement
                .Elements(itemDefinitionGroupXName)
                .SelectMany(group => group.Elements(projectReferenceXName))
                .SelectMany(element => element.Elements(linkLibraryDependenciesXName))
                .Select(element => element.Value.Trim() switch
                {
                    "true" => true,
                    "false" => false,
                    _ => throw new CatastrophicFailureException(
                        $"Invalid value for LinkLibraryDependencies: {element.Value}")
                })
                .Distinct()
                .SingleOrDefaultWithException(true,
                    () => throw new CatastrophicFailureException(
                        "LinkLibraryDependencies property is inconsistent between configurations"));

        var requiresQtMoc =
            projectElement
            .Elements(itemGroupXName)
            .SelectMany(group => group.Elements(qtMocXName))
            .Any();

        var requiresQtUic =
            projectElement
            .Elements(itemGroupXName)
            .SelectMany(group => group.Elements(qtUicXName))
            .Any();

        var requiresQtRcc =
            projectElement
            .Elements(itemGroupXName)
            .SelectMany(group => group.Elements(qtRccXName))
            .Any();

        Dictionary<string, Dictionary<MSBuildProjectConfig, string>> compilerSettings = [];
        Dictionary<string, Dictionary<MSBuildProjectConfig, string>> linkerSettings = [];
        Dictionary<string, Dictionary<MSBuildProjectConfig, string>> otherSettings = [];

        foreach (var projectConfig in projectConfigurations)
        {
            var itemDefinitionGroups =
                projectElement
                    .Elements(itemDefinitionGroupXName)
                    .Where(group => group.Attribute("Condition") == null ||
                                    Regex.IsMatch(group.Attribute("Condition")!.Value,
                                        $@"'\$\(Configuration\)\|\$\(Platform\)'\s*==\s*'{Regex.Escape(projectConfig.Name)}'"))
                    .ToList();

            var propertyGroups =
                projectElement
                    .Elements(propertyGroupXName)
                    .Where(group => group.Attribute("Condition") == null ||
                                    Regex.IsMatch(group.Attribute("Condition")!.Value,
                                        $@"'\$\(Configuration\)\|\$\(Platform\)'\s*==\s*'{Regex.Escape(projectConfig.Name)}'"))
                    .ToList();

            var projectConfigCompilerSettings =
                itemDefinitionGroups
                .SelectMany(group => group.Elements(clCompileXName))
                .SelectMany(element => element.Elements())
                .ToDictionaryKeepingLast(element => element.Name.LocalName, element => element.Value.Trim());

            var projectConfigLinkerSettings =
                itemDefinitionGroups
                .SelectMany(group => group.Elements())
                .Where(element => element.Name == linkXName || element.Name == libXName)
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

        var targetName = ParseSetting("TargetName", otherSettings, Path.GetFileNameWithoutExtension(projectPath));
        var additionalIncludeDirectories = ParseMultiSetting("AdditionalIncludeDirectories", ';', compilerSettings, []);
        var publicIncludeDirectories = ParseMultiSetting("PublicIncludeDirectories", ';', otherSettings, []);
        var additionalLibraryDirectories = ParseMultiSetting("AdditionalLibraryDirectories", ';', linkerSettings, []);
        var additionalDependencies = ParseMultiSetting("AdditionalDependencies", ';', linkerSettings, []);
        var preprocessorDefinitions = ParseMultiSetting("PreprocessorDefinitions", ';', compilerSettings, []);
        var additionalOptions = ParseMultiSetting("AdditionalOptions", ' ', compilerSettings, []);
        var characterSet = ParseSetting("CharacterSet", otherSettings, "NotSet");
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
                    var match = Regex.Match(import, @"conan_([A-Za-z0-9-_]+)\.props");
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
            HeaderFiles = headerFiles.ToArray(),
            TargetName = targetName,
            AdditionalIncludeDirectories = additionalIncludeDirectories,
            PublicIncludeDirectories = publicIncludeDirectories,
            AdditionalLibraryDirectories = additionalLibraryDirectories,
            AdditionalDependencies = additionalDependencies,
            PreprocessorDefinitions = preprocessorDefinitions,
            AdditionalOptions = additionalOptions,
            CharacterSet = characterSet,
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

        MSBuildConfigDependentSetting<string[]> ParseMultiSetting(
            string property,
            char separator,
            Dictionary<string, Dictionary<MSBuildProjectConfig, string>> settings,
            string[] defaultValue)
        {
            var parser = (string value) =>
                value
                .Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Except([$"%({property})"])
                .Distinct()
                .ToArray();

            return new(property, defaultValue, settings.GetValueOrDefault(property, []), parser);
        }
    }
}
