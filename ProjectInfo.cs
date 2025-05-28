using System.Text.RegularExpressions;
using System.Xml.Linq;

class ProjectInfo
{
    public required string AbsoluteProjectPath { get; init; }
    public required string ProjectName { get; init; }
    public string? UniqueName { get; set; }
    public required string[] Languages { get; init; }
    public required string ConfigurationType { get; init; }
    public required string? LanguageStandard { get; init; }
    public required string[] SourceFiles { get; init; }
    public required ConfigDependentMultiSetting IncludePaths { get; init; }
    public required ConfigDependentMultiSetting LinkerPaths { get; init; }
    public required ConfigDependentMultiSetting Libraries { get; init; }
    public required ConfigDependentMultiSetting Defines { get; init; }
    public required ConfigDependentMultiSetting Options { get; init; }
    public required ProjectReference[] ProjectReferences { get; init; }
    public required string? LinkerSubsystem { get; init; }
    public required bool LinkLibraryDependenciesEnabled { get; init; }
    public required bool RequiresMoc { get; init; }
    public required QtModule[] QtModules { get; init; }
    public required ConanPackage[] ConanPackages { get; init; }

    public static ProjectInfo ParseProjectFile(string projectPath, ConanPackageInfoRepository conanPackageInfoRepository)
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
        var libXName = XName.Get("Lib", msbuildNamespace);
        var qtModulesXName = XName.Get("QtModules", msbuildNamespace);
        var importXName = XName.Get("Import", msbuildNamespace);
        var importGroupXName = XName.Get("ImportGroup", msbuildNamespace);
        var projectReferenceXName = XName.Get("ProjectReference", msbuildNamespace);
        var linkLibraryDependenciesXName = XName.Get("LinkLibraryDependencies", msbuildNamespace);
        var qtMocXName = XName.Get("QtMoc", msbuildNamespace);

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
                .SelectMany(group => group.Elements(clCompileXName))
                .Select(element => element.Attribute("Include")!.Value.Trim())
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

        var requiresMoc =
            projectElement
            .Elements(itemGroupXName)
            .SelectMany(group => group.Elements(qtMocXName))
            .Any();

        Dictionary<string, Dictionary<string, string>> compilerSettings = [];
        Dictionary<string, Dictionary<string, string>> linkerSettings = [];
        Dictionary<string, Dictionary<string, string>> otherSettings = [];

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

        var languageStandard = 
            compilerSettings.GetValueOrDefault("LanguageStandard")?.Values
            .Distinct()
            .SingleOrDefaultWithException(null, () => throw new CatastrophicFailureException("LanguageStandard property is inconsistent between configurations"));
        if (languageStandard == null)
            Console.WriteLine("Warning: Language standard could not be determined.");

        var includePaths = ConfigDependentMultiSetting.Parse(
            compilerSettings.GetValueOrDefault("AdditionalIncludeDirectories"),
            value => ParseList(value, ';', "%(AdditionalIncludeDirectories)"));

        var linkerPaths = ConfigDependentMultiSetting.Parse(
            linkerSettings.GetValueOrDefault("AdditionalLibraryDirectories"), 
            value => ParseList(value, ';', "%(AdditionalLibraryDirectories)"));

        var libraries = ConfigDependentMultiSetting.Parse(
            linkerSettings.GetValueOrDefault("AdditionalDependencies"), 
            value => ParseList(value, ';', "%(AdditionalDependencies)"));

        var defines = ConfigDependentMultiSetting.Parse(
            compilerSettings.GetValueOrDefault("PreprocessorDefinitions"), 
            value => ParseList(value, ';', "%(PreprocessorDefinitions)"));

        var options = ConfigDependentMultiSetting.Parse(
            compilerSettings.GetValueOrDefault("AdditionalOptions"), 
            value => ParseList(value, ' ', "%(AdditionalOptions)"));

        var characterSet = ConfigDependentSetting.Parse(
            otherSettings.GetValueOrDefault("CharacterSet"));

        var disableSpecificWarnings = ConfigDependentMultiSetting.Parse(
            compilerSettings.GetValueOrDefault("DisableSpecificWarnings"),
            value => ParseList(value, ';', "%(DisableSpecificWarnings)"));

        var treatSpecificWarningsAsErrors = ConfigDependentMultiSetting.Parse(
            compilerSettings.GetValueOrDefault("TreatSpecificWarningsAsErrors"),
            value => ParseList(value, ';', "%(TreatSpecificWarningsAsErrors)"));

        var treatWarningAsError = ConfigDependentSetting.Parse(
            compilerSettings.GetValueOrDefault("TreatWarningAsError"));

        var warningLevel = ConfigDependentSetting.Parse(
            compilerSettings.GetValueOrDefault("WarningLevel"));

        var externalWarningLevel = ConfigDependentSetting.Parse(
            compilerSettings.GetValueOrDefault("ExternalWarningLevel"));

        var treatAngleIncludeAsExternal = ConfigDependentSetting.Parse(
            compilerSettings.GetValueOrDefault("TreatAngleIncludeAsExternal"));

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

        var linkerSubsystem =
            linkerSettings.GetValueOrDefault("SubSystem")?.Values
                .Distinct()
                .SingleOrDefaultWithException(null,
                    () => throw new CatastrophicFailureException(
                        "SubSystem property is inconsistent between configurations"));

        defines = ApplyCharacterSetSetting(characterSet, defines);
        options = ApplyDisableSpecificWarnings(disableSpecificWarnings, options);
        options = ApplyTreatSpecificWarningsAsErrors(treatSpecificWarningsAsErrors, options);
        options = ApplyTreatWarningAsError(treatWarningAsError, options);
        options = ApplyWarningLevel(warningLevel, options);
        options = ApplyExternalWarningLevel(externalWarningLevel, options);
        options = ApplyTreatAngleIncludeAsExternal(treatAngleIncludeAsExternal, options);

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
            LinkerSubsystem = linkerSubsystem,
            LinkLibraryDependenciesEnabled = linkLibraryDependenciesEnabled,
            RequiresMoc = requiresMoc,
            QtModules = qtModules.Select(module => QtModuleInfoRepository.GetQtModuleInfo(module)).ToArray(),
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

    static ConfigDependentMultiSetting ApplyCharacterSetSetting(ConfigDependentSetting characterSet, ConfigDependentMultiSetting defines)
    {
        return defines.Map((defines, charSet) => charSet switch
        {
            "Unicode" => [.. defines, "UNICODE", "_UNICODE"],
            "MultiByte" => [.. defines, "_MBCS"],
            "NotSet" or "" or null => defines,
            _ => throw new CatastrophicFailureException($"Invalid value for CharacterSet: {charSet}")
        }, characterSet);
    }

    static ConfigDependentMultiSetting ApplyDisableSpecificWarnings(ConfigDependentMultiSetting disableSpecificWarnings, ConfigDependentMultiSetting options)
    {
        return options.Map(
            (options, warnings) => [.. options, .. warnings.Select(w => Convert.ToInt32(w)).Select(w => $"/wd{w}")],
            disableSpecificWarnings);
    }

    static ConfigDependentMultiSetting ApplyTreatSpecificWarningsAsErrors(ConfigDependentMultiSetting treatSpecificWarningsAsErrors, ConfigDependentMultiSetting options)
    {
        return options.Map(
            (options, warnings) => [.. options, .. warnings.Select(w => Convert.ToInt32(w)).Select(w => $"/we{w}")],
            treatSpecificWarningsAsErrors);
    }

    static ConfigDependentMultiSetting ApplyTreatWarningAsError(ConfigDependentSetting treatWarningAsError, ConfigDependentMultiSetting options)
    {
        return options.Map((options, treatAsError) => (treatAsError?.ToLowerInvariant()) switch
        {
            "true" => [.. options, "/WX"],
            "false" or "" or null => options,
            _ => throw new CatastrophicFailureException($"Invalid value for TreatWarningAsError: {treatAsError}"),
        }, treatWarningAsError);
    }

    static ConfigDependentMultiSetting ApplyWarningLevel(ConfigDependentSetting warningLevel, ConfigDependentMultiSetting options)
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
        }, warningLevel);
    }

    static ConfigDependentMultiSetting ApplyExternalWarningLevel(ConfigDependentSetting externalWarningLevel, ConfigDependentMultiSetting options)
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
        }, externalWarningLevel);
    }

    static ConfigDependentMultiSetting ApplyTreatAngleIncludeAsExternal(ConfigDependentSetting treatAngleIncludeAsExternal, ConfigDependentMultiSetting options)
    {
        return options.Map((options, treatAsExternal) => (treatAsExternal?.ToLowerInvariant()) switch
        {
            "true" => [.. options, "/external:anglebrackets"],
            "false" or "" or null => options,
            _ => throw new CatastrophicFailureException($"Invalid value for TreatAngleIncludeAsExternal: {treatAsExternal}"),
        }, treatAngleIncludeAsExternal);
    }

    public ISet<ProjectInfo> GetAllReferencedProjects(IEnumerable<ProjectInfo> allProjects)
    {
        var referencedProjects = new HashSet<ProjectInfo>();

        void GetAllReferencedProjectsInner(ProjectInfo projectInfo)
        {
            foreach (var projectReference in projectInfo.ProjectReferences)
                if (referencedProjects.Add(projectReference.ProjectFileInfo!))
                    GetAllReferencedProjectsInner(projectReference.ProjectFileInfo!);

        }

        GetAllReferencedProjectsInner(this);

        return referencedProjects;
    }
}

record ConfigDependentSetting
{
    public required string? Common { get; init; }
    public required string? Debug { get; init; }
    public required string? Release { get; init; }
    public required string? X86 { get; init; }
    public required string? X64 { get; init; }

    public static readonly ConfigDependentSetting Empty = new()
    {
        Common = null,
        Debug = null,
        Release = null,
        X86 = null,
        X64 = null
    };

    public static ConfigDependentSetting Parse(Dictionary<string, string>? settings)
    {
        if (settings == null || settings.Count == 0)
            return Empty;

        var allSettingValues = settings.Values.Distinct().ToArray();

        var commonSettingValue = allSettingValues.FirstOrDefault(s => settings.All(kvp => kvp.Value == s));

        string? FilterValues(Func<string, bool> selector)
        {
            return allSettingValues
                .Where(s => settings.All(kvp => selector(kvp.Key) == (kvp.Value == s)))
                .FirstOrDefault(s => s != commonSettingValue);
        }

        var result = new ConfigDependentSetting
        {
            Common = commonSettingValue,
            Debug = FilterValues(config => config.StartsWith("Debug|")),
            Release = FilterValues(config => config.StartsWith("Release|")),
            X86 = FilterValues(config => config.EndsWith("|Win32") || config.EndsWith("|x86")),
            X64 = FilterValues(config => config.EndsWith("|x64"))
        };

        var skippedSettings = settings.Values
            .Except([result.Common, result.Debug, result.Release, result.X86, result.X64])
            .ToArray();
        if (skippedSettings.Length > 0)
            Console.WriteLine($"Warning: some settings were skipped: {string.Join(", ", skippedSettings)}");

        return result;
    }

    public bool IsEmpty => Common == null && Debug == null && Release == null && X86 == null && X64 == null;
}

record ConfigDependentMultiSetting
{
    public required string[] Common { get; init; }
    public required string[] Debug { get; init; }
    public required string[] Release { get; init; }
    public required string[] X86 { get; init; }
    public required string[] X64 { get; init; }

    public static readonly ConfigDependentMultiSetting Empty = new()
    {
        Common = [],
        Debug = [],
        Release = [],
        X86 = [],
        X64 = []
    };

    public static ConfigDependentMultiSetting Parse(Dictionary<string, string>? settings, Func<string, string[]> parser)
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

        var result = new ConfigDependentMultiSetting
        {
            Common = commonSettingValues,
            Debug = FilterValues(config => config.StartsWith("Debug|")),
            Release = FilterValues(config => config.StartsWith("Release|")),
            X86 = FilterValues(config => config.EndsWith("|Win32") || config.EndsWith("|x86")),
            X64 = FilterValues(config => config.EndsWith("|x64"))
        };

        var skippedSettings = parsedSettings.Values
            .SelectMany(s => s)
            .Except([.. result.Common, .. result.Debug, .. result.Release, .. result.X86, .. result.X64])
            .ToArray();
        if (skippedSettings.Length > 0)
            Console.WriteLine($"Warning: some settings were skipped: {string.Join(", ", skippedSettings)}");

        return result;
    }

    public bool IsEmpty => !Enumerable.Any([.. Common, .. Debug, .. Release, .. X86, .. X64]);
}

class ProjectReference
{
    public required string Path { get; init; }
    public ProjectInfo? ProjectFileInfo { get; set; }
}
