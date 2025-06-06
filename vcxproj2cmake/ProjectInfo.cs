﻿using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace vcxproj2cmake;

class ProjectInfo
{
    public required string AbsoluteProjectPath { get; init; }
    public required string ProjectName { get; init; }
    public string? UniqueName { get; set; }
    public required string[] ProjectConfigurations { get; init; }
    public required string[] Languages { get; init; }
    public required string ConfigurationType { get; init; }
    public required string CppLanguageStandard { get; init; }
    public required string CLanguageStandard { get; init; }
    public required string[] SourceFiles { get; init; }
    public required ConfigDependentMultiSetting IncludePaths { get; init; }
    public required ConfigDependentMultiSetting PublicIncludePaths { get; init; }
    public required ConfigDependentMultiSetting LinkerPaths { get; init; }
    public required ConfigDependentMultiSetting Libraries { get; init; }
    public required ConfigDependentMultiSetting Defines { get; init; }
    public required ConfigDependentMultiSetting Options { get; init; }
    public required ProjectReference[] ProjectReferences { get; init; }
    public required string? LinkerSubsystem { get; init; }
    public required bool LinkLibraryDependenciesEnabled { get; init; }
    public required bool IsHeaderOnlyLibrary { get; init; }
    public required ConfigDependentSetting PrecompiledHeaderFile { get; init; }
    public required bool UsesOpenMP { get; init; }
    public required int? QtVersion { get; init; }
    public required bool RequiresQtMoc { get; init; }
    public required bool RequiresQtUic { get; init; }
    public required bool RequiresQtRcc { get; init; }
    public required QtModule[] QtModules { get; init; }
    public required ConanPackage[] ConanPackages { get; init; }

    public static ProjectInfo ParseProjectFile(string projectPath, int? qtVersion, ConanPackageInfoRepository conanPackageInfoRepository, ILogger logger)
    {
        logger.LogInformation($"Parsing {projectPath}");

        var msbuildNamespace = "http://schemas.microsoft.com/developer/msbuild/2003";
        var clCompileXName = XName.Get("ClCompile", msbuildNamespace);
        var clIncludeXName = XName.Get("ClInclude", msbuildNamespace);
        var configurationTypeXName = XName.Get("ConfigurationType", msbuildNamespace);
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

        var doc = XDocument.Load(projectPath);
        var projectElement = doc.Element(projectXName)!;

        var projectConfigurations =
            projectElement
                .Elements(itemGroupXName)
                .SelectMany(group => group.Elements(projectConfigurationXName))
                .Select(element => element.Attribute("Include")!.Value.Trim())
                .ToList();

        var configurationType =
            projectElement
                .Elements(propertyGroupXName)
                .SelectMany(group => group.Elements(configurationTypeXName))
                .Select(element => element.Value.Trim())
                .Distinct()
                .SingleWithException(() =>
                    throw new CatastrophicFailureException(
                        "Configuration type is absent or inconsistent between configurations"));

        var sourceFiles =
            projectElement
                .Elements(itemGroupXName)
                .SelectMany(group =>
                    group.Elements(clCompileXName)
                    .Concat(group.Elements(qtUicXName))
                    .Concat(group.Elements(qtRccXName)))
                .Select(element => element.Attribute("Include")!.Value.Trim())
                .ToList();

        var headerFiles =
            projectElement
                .Elements(itemGroupXName)
                .SelectMany(group => group.Elements(clIncludeXName))
                .Select(element => element.Attribute("Include")!.Value.Trim())
                .ToList();

        // We don't rely on configurationType to determine if the project is a header-only library
        // since there is no specific configuration type for header-only libraries in MSBuild.
        var isHeaderOnlyLibrary = sourceFiles.Count == 0 && headerFiles.Count > 0;

        var qtModules =
            projectElement
                .Elements(propertyGroupXName)
                .SelectMany(group => group.Elements(qtModulesXName))
                .Select(element => element.Value.Trim())
                .Distinct()
                .SingleOrDefaultWithException(string.Empty,
                    () => throw new CatastrophicFailureException("Qt modules are inconsistent between configurations"))
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (qtModules.Any() && qtVersion == null)
            throw new CatastrophicFailureException("Project uses Qt but no Qt version is set. Specify the version with --qt-version.");

        var imports =
            projectElement
                .Elements(importXName)
                .Concat(projectElement.Elements(importGroupXName).SelectMany(group => group.Elements(importXName)))
                .Select(import => import.Attribute("Project")!.Value.Trim())
                .ToList();

        var projectReferences =
            projectElement
                .Elements(itemGroupXName)
                .SelectMany(group => group.Elements(projectReferenceXName))
                .Select(element => element.Attribute("Include")!.Value.Trim())
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

        Dictionary<string, Dictionary<string, string>> compilerSettings = [];
        Dictionary<string, Dictionary<string, string>> linkerSettings = [];
        Dictionary<string, Dictionary<string, string>> otherSettings = [];
        List<string> processedProjectConfigurations = [];

        foreach (var projectConfig in projectConfigurations)
        {
            if (!Config.IsMSBuildProjectConfigNameSupported(projectConfig))
            {
                logger.LogWarning($"Skipping unsupported project configuration: {projectConfig}");
                continue;
            }

            processedProjectConfigurations.Add(projectConfig);

            var itemDefinitionGroups =
                projectElement
                    .Elements(itemDefinitionGroupXName)
                    .Where(group => group.Attribute("Condition") == null ||
                                    Regex.IsMatch(group.Attribute("Condition")!.Value,
                                        $@"'\$\(Configuration\)\|\$\(Platform\)'\s*==\s*'{Regex.Escape(projectConfig)}'"))
                    .ToList();

            var propertyGroups =
                projectElement
                    .Elements(propertyGroupXName)
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
                    projectConfigCompilerSettings[element.Name.LocalName] = element.Value.Trim();
                }

                var projectConfigLinkerSettings = new Dictionary<string, string>();
                foreach (var element in group.Elements()
                             .Where(element => element.Name == linkXName || element.Name == libXName)
                             .SelectMany(element => element.Elements()))
                {
                    projectConfigLinkerSettings[element.Name.LocalName] = element.Value.Trim();
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

            var projectConfigOtherSettings = new Dictionary<string, string>();
            foreach (var element in propertyGroups.SelectMany(element => element.Elements()))
            {
                projectConfigOtherSettings[element.Name.LocalName] = element.Value.Trim();
            }

            foreach (var setting in projectConfigOtherSettings)
            {
                otherSettings.TryAdd(setting.Key, []);
                otherSettings[setting.Key][projectConfig] = setting.Value;
            }
        }

        var cppLanguageStandard = GetCommonSetting("LanguageStandard", compilerSettings) ?? "Default";
        var cLanguageStandard = GetCommonSetting("LanguageStandard_C", compilerSettings) ?? "Default";

        var includePaths = ParseMultiSetting("AdditionalIncludeDirectories", ';', compilerSettings, []);
        var publicIncludePaths = ParseMultiSetting("PublicIncludeDirectories", ';', otherSettings, []);
        var linkerPaths = ParseMultiSetting("AdditionalLibraryDirectories", ';', linkerSettings, []);
        var libraries = ParseMultiSetting("AdditionalDependencies", ';', linkerSettings, []);
        var defines = ParseMultiSetting("PreprocessorDefinitions", ';', compilerSettings, []);
        var options = ParseMultiSetting("AdditionalOptions", ' ', compilerSettings, []);
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
                .Select(packageName => conanPackageInfoRepository.GetConanPackageInfo(packageName!))
                .ToArray();

        string? linkerSubsystem = GetCommonSetting("SubSystem", linkerSettings);

        publicIncludePaths = ApplyAllProjectIncludesArePublic(allProjectIncludesArePublic, publicIncludePaths, projectConfigurations, logger);
        defines = ApplyCharacterSetSetting(characterSet, defines, projectConfigurations, logger);
        options = ApplyDisableSpecificWarnings(disableSpecificWarnings, options, projectConfigurations, logger);
        options = ApplyTreatSpecificWarningsAsErrors(treatSpecificWarningsAsErrors, options, projectConfigurations, logger);
        options = ApplyTreatWarningAsError(treatWarningAsError, options, projectConfigurations, logger);
        options = ApplyWarningLevel(warningLevel, options, projectConfigurations, logger);
        options = ApplyExternalWarningLevel(externalWarningLevel, options, projectConfigurations, logger);
        options = ApplyTreatAngleIncludeAsExternal(treatAngleIncludeAsExternal, options, projectConfigurations, logger);
        libraries = ApplyOpenMPSupport(openMPSupport, libraries, projectConfigurations, logger);

        return new ProjectInfo
        {
            AbsoluteProjectPath = Path.GetFullPath(projectPath),
            ProjectName = Path.GetFileNameWithoutExtension(projectPath),
            ProjectConfigurations = processedProjectConfigurations.ToArray(),
            Languages = DetectLanguages(sourceFiles, logger),
            ConfigurationType = configurationType,
            CppLanguageStandard = cppLanguageStandard,
            CLanguageStandard = cLanguageStandard,
            SourceFiles = sourceFiles.ToArray(),
            IncludePaths = includePaths,
            PublicIncludePaths = publicIncludePaths,
            LinkerPaths = linkerPaths,
            Libraries = libraries,
            Defines = defines,
            Options = options,
            ProjectReferences = projectReferences.Select(pr => new ProjectReference { Path = pr }).ToArray(),
            LinkerSubsystem = linkerSubsystem,
            LinkLibraryDependenciesEnabled = linkLibraryDependenciesEnabled,
            IsHeaderOnlyLibrary = isHeaderOnlyLibrary,
            PrecompiledHeaderFile = precompiledHeaderFile.Map((file, mode) => mode == "Use" ? file : null, precompiledHeader, projectConfigurations, logger),
            UsesOpenMP = openMPSupport.Values.Values.Contains("true", StringComparer.OrdinalIgnoreCase),
            QtVersion = qtVersion,
            RequiresQtMoc = requiresQtMoc,
            RequiresQtUic = requiresQtUic,
            RequiresQtRcc = requiresQtRcc,
            QtModules = qtModules.Select(module => QtModuleInfoRepository.GetQtModuleInfo(module, qtVersion!.Value)).ToArray(),
            ConanPackages = conanPackages
        };

        string? GetCommonSetting(string property, Dictionary<string, Dictionary<string, string>> settings)
        {
            return settings.GetValueOrDefault(property)?.Values
                .Distinct()
                .SingleOrDefaultWithException(null, () => throw new CatastrophicFailureException($"{property} property is inconsistent between configurations"));
        }

        ConfigDependentSetting ParseSetting(
            string property,
            Dictionary<string, Dictionary<string, string>> settings,
            string defaultValue)
        {
            return ConfigDependentSetting.Parse(
                settings.GetValueOrDefault(property),
                property,
                defaultValue,
                processedProjectConfigurations,
                logger);
        }

        ConfigDependentMultiSetting ParseMultiSetting(
            string property,
            char separator,
            Dictionary<string, Dictionary<string, string>> settings,
            string[] defaultValue)
        {
            var parser = (string value) =>
                value
                .Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Except([$"%({property})"])
                .Distinct()
                .ToArray();

            return ConfigDependentMultiSetting.Parse(
                settings.GetValueOrDefault(property)?.ToDictionary(kvp => kvp.Key, kvp => parser(kvp.Value)),
                property,
                defaultValue,
                processedProjectConfigurations,
                logger);
        }
    }

    static string[] DetectLanguages(IEnumerable<string> sourceFiles, ILogger logger)
    {
        List<string> result = [];

        if (sourceFiles.Any(file => file.EndsWith(".c", StringComparison.OrdinalIgnoreCase)))
            result.Add("C");
        if (sourceFiles.Any(file => Regex.IsMatch(file, @"\.(cpp|cxx|c\+\+|cc|hpp)$", RegexOptions.IgnoreCase)))
            result.Add("CXX");

        if (result.Count == 0)
            logger.LogWarning("Could not detect languages for project");

        return result.ToArray();
    }

    static ConfigDependentMultiSetting ApplyCharacterSetSetting(ConfigDependentSetting characterSet, ConfigDependentMultiSetting defines, IEnumerable<string> projectConfigurations, ILogger logger)
    {
        return defines.Map((defines, charSet) => charSet switch
        {
            "Unicode" => [.. defines, "UNICODE", "_UNICODE"],
            "MultiByte" => [.. defines, "_MBCS"],
            "NotSet" or "" or null => defines,
            _ => throw new CatastrophicFailureException($"Invalid value for CharacterSet: {charSet}")
        }, characterSet, projectConfigurations, logger);
    }

    static ConfigDependentMultiSetting ApplyDisableSpecificWarnings(ConfigDependentMultiSetting disableSpecificWarnings, ConfigDependentMultiSetting options, IEnumerable<string> projectConfigurations, ILogger logger)
    {
        return options.Map(
            (options, warnings) => [.. options, .. warnings.Select(w => Convert.ToInt32(w)).Select(w => $"/wd{w}")],
            disableSpecificWarnings, projectConfigurations, logger);
    }

    static ConfigDependentMultiSetting ApplyTreatSpecificWarningsAsErrors(ConfigDependentMultiSetting treatSpecificWarningsAsErrors, ConfigDependentMultiSetting options, IEnumerable<string> projectConfigurations, ILogger logger)
    {
        return options.Map(
            (options, warnings) => [.. options, .. warnings.Select(w => Convert.ToInt32(w)).Select(w => $"/we{w}")],
            treatSpecificWarningsAsErrors, projectConfigurations, logger);
    }

    static ConfigDependentMultiSetting ApplyTreatWarningAsError(ConfigDependentSetting treatWarningAsError, ConfigDependentMultiSetting options, IEnumerable<string> projectConfigurations, ILogger logger)
    {
        return options.Map((options, treatAsError) => (treatAsError?.ToLowerInvariant()) switch
        {
            "true" => [.. options, "/WX"],
            "false" or "" or null => options,
            _ => throw new CatastrophicFailureException($"Invalid value for TreatWarningAsError: {treatAsError}"),
        }, treatWarningAsError, projectConfigurations, logger);
    }

    static ConfigDependentMultiSetting ApplyWarningLevel(ConfigDependentSetting warningLevel, ConfigDependentMultiSetting options, IEnumerable<string> projectConfigurations, ILogger logger)
    {
        return options.Map((options, level) => level switch
        {
            "TurnOffAllWarnings" => [.. options, "/W0"],
            "Level1" => [.. options, "/W1"],
            "Level2" => [.. options, "/W2"],
            "Level3" => [.. options, "/W3"],
            "Level4" => [.. options, "/W4"],
            "" or null => options,
            _ => throw new CatastrophicFailureException($"Invalid value for WarningLevel: {level}")
        }, warningLevel, projectConfigurations, logger);
    }

    static ConfigDependentMultiSetting ApplyExternalWarningLevel(ConfigDependentSetting externalWarningLevel, ConfigDependentMultiSetting options, IEnumerable<string> projectConfigurations, ILogger logger)
    {
        return options.Map((options, level) => level switch
        {
            "TurnOffAllWarnings" => [.. options, "/external:W0"],
            "Level1" => [.. options, "/external:W1"],
            "Level2" => [.. options, "/external:W2"],
            "Level3" => [.. options, "/external:W3"],
            "Level4" => [.. options, "/external:W4"],
            "" or null => options,
            _ => throw new CatastrophicFailureException($"Invalid value for ExternalWarningLevel: {level}")
        }, externalWarningLevel, projectConfigurations, logger);
    }

    static ConfigDependentMultiSetting ApplyTreatAngleIncludeAsExternal(ConfigDependentSetting treatAngleIncludeAsExternal, ConfigDependentMultiSetting options, IEnumerable<string> projectConfigurations, ILogger logger)
    {
        return options.Map((options, treatAsExternal) => (treatAsExternal?.ToLowerInvariant()) switch
        {
            "true" => [.. options, "/external:anglebrackets"],
            "false" or "" or null => options,
            _ => throw new CatastrophicFailureException($"Invalid value for TreatAngleIncludeAsExternal: {treatAsExternal}"),
        }, treatAngleIncludeAsExternal, projectConfigurations, logger);
    }

    static ConfigDependentMultiSetting ApplyAllProjectIncludesArePublic(ConfigDependentSetting allProjectIncludesArePublic, ConfigDependentMultiSetting publicIncludeDirectories, IEnumerable<string> projectConfigurations, ILogger logger)
    {
        return publicIncludeDirectories.Map((directories, allArePublic) => (allArePublic?.ToLowerInvariant()) switch
        {
            "true" => [.. directories, "$(ProjectDir)"],
            "false" or "" or null => directories,
            _ => throw new CatastrophicFailureException($"Invalid value for AllProjectIncludesArePublic: {allArePublic}"),
        }, allProjectIncludesArePublic, projectConfigurations, logger);
    }

    static ConfigDependentMultiSetting ApplyOpenMPSupport(ConfigDependentSetting openMPSupport, ConfigDependentMultiSetting libraries, IEnumerable<string> projectConfigurations, ILogger logger)
    {
        return libraries.Map((libs, openMP) => (openMP?.ToLowerInvariant()) switch
        {
            "true" => [.. libs, "OpenMP::OpenMP_CXX"],
            "false" or "" or null => libs,
            _ => throw new CatastrophicFailureException($"Invalid value for OpenMPSupport: {openMP}"),
        }, openMPSupport, projectConfigurations, logger);
    }

    public ISet<ProjectInfo> GetAllReferencedProjects(IEnumerable<ProjectInfo> allProjects)
    {
        var referencedProjects = new HashSet<ProjectInfo>();

        void GetAllReferencedProjectsInner(ProjectInfo projectInfo)
        {
            foreach (var projectReference in projectInfo.ProjectReferences)
                if (referencedProjects.Add(projectReference.ProjectInfo!))
                    GetAllReferencedProjectsInner(projectReference.ProjectInfo!);

        }

        GetAllReferencedProjectsInner(this);

        return referencedProjects;
    }
}

class ProjectReference
{
    public required string Path { get; init; }
    public ProjectInfo? ProjectInfo { get; set; }
}
