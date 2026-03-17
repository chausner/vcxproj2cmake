using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace vcxproj2cmake;

class CMakeProject
{
    public MSBuildProject MSBuildProject { get; }
    public string AbsoluteProjectPath { get; set; }
    public string ProjectName { get; set; }
    public MSBuildProjectConfig[] ProjectConfigurations { get; set; }
    public string[] Languages { get; set; }
    public CMakeTargetType TargetType { get; set; }
    public IList<CMakeFindPackage> FindPackages { get; set; }
    public CMakeConfigDependentMultiSetting CompileFeatures { get; set; }
    public CMakeExpression[] SourceFiles { get; set; }
    public CMakeExpression OutputName { get; set; }
    public CMakeConfigDependentMultiSetting IncludePaths { get; set; }
    public CMakeConfigDependentMultiSetting PublicIncludePaths { get; set; }
    public CMakeConfigDependentMultiSetting LinkerPaths { get; set; }
    public CMakeConfigDependentMultiSetting Libraries { get; set; }
    public CMakeConfigDependentMultiSetting Defines { get; set; }
    public CMakeConfigDependentMultiSetting Options { get; set; }
    public OrderedDictionary<string, CMakeExpression> Properties { get; set; }
    public CMakeConfigDependentSetting ModuleDefinitionFile { get; set; }
    public CMakeProjectReference[] ProjectReferences { get; set; }
    public bool IsWin32Executable { get; set; }
    public CMakeConfigDependentSetting PrecompiledHeaderFile { get; set; }

    public CMakeProject(MSBuildProject project, int? qtVersion, bool includeHeaders, ConanPackageInfoRepository conanPackageInfoRepository, ILogger logger)
    {
        logger.LogInformation($"Processing project {project.AbsoluteProjectPath}");

        var supportedProjectConfigurations = FilterSupportedProjectConfigurations(project.ProjectConfigurations, logger);

        MSBuildProject = project;
        AbsoluteProjectPath = project.AbsoluteProjectPath;
        ProjectName = project.ProjectName;
        ProjectConfigurations = supportedProjectConfigurations;
        Languages = DetectLanguages(project.SourceFiles, logger);
        TargetType = DetermineTargetType(project); 
        FindPackages = [];
        CompileFeatures = new("CompileFeatures", []);

        var normalizedSourceFiles = project.SourceFiles.Select(value => TranslateAndNormalize(CMakeExpression.Literal(value), "SourceFiles", logger));
        var normalizedHeaderFiles = project.HeaderFiles.Select(value => TranslateAndNormalize(CMakeExpression.Literal(value), "HeaderFiles", logger));
        SourceFiles = normalizedSourceFiles.Concat(includeHeaders ? normalizedHeaderFiles : []).ToArray();

        OutputName = CMakeExpression.Literal(project.ProjectName);  // may get overridden in ApplyTargetName
        var mergedIncludeDirectories = MergeIncludeDirectories(project, supportedProjectConfigurations);
        IncludePaths = new CMakeConfigDependentMultiSetting(mergedIncludeDirectories, supportedProjectConfigurations, logger)
            .Map(values => values.Select(value => TranslateAndNormalize(value, "AdditionalIncludeDirectories+IncludePath", logger)).ToArray(), supportedProjectConfigurations, logger);
        PublicIncludePaths = new CMakeConfigDependentMultiSetting(project.PublicIncludeDirectories, supportedProjectConfigurations, logger)
            .Map(values => values.Select(value => TranslateAndNormalize(value, "PublicIncludeDirectories", logger)).ToArray(), supportedProjectConfigurations, logger);
        var mergedLibraryDirectories = MergeLibraryDirectories(project, supportedProjectConfigurations);
        LinkerPaths = new CMakeConfigDependentMultiSetting(mergedLibraryDirectories, supportedProjectConfigurations, logger)
            .Map(values => values.Select(value => TranslateAndNormalize(value, "AdditionalLibraryDirectories+LibraryPath", logger)).ToArray(), supportedProjectConfigurations, logger);
        Libraries = new CMakeConfigDependentMultiSetting(project.AdditionalDependencies, supportedProjectConfigurations, logger)
            .Map(values => values.Select(value => TranslateAndNormalize(value, "AdditionalDependencies", logger)).ToArray(), supportedProjectConfigurations, logger);
        Defines = new CMakeConfigDependentMultiSetting(project.PreprocessorDefinitions, supportedProjectConfigurations, logger)
            .Map(values => values.Select(value => TranslateMSBuildMacros(value, "PreprocessorDefinitions", logger)).ToArray(), supportedProjectConfigurations, logger);
        Options = new CMakeConfigDependentMultiSetting(project.AdditionalOptions, supportedProjectConfigurations, logger)
            .Map(values => values.Select(value => TranslateMSBuildMacros(value, "AdditionalOptions", logger)).ToArray(), supportedProjectConfigurations, logger);
        ModuleDefinitionFile = new CMakeConfigDependentSetting(project.ModuleDefinitionFile, supportedProjectConfigurations, logger)
            .Map(value => value != null ? TranslateAndNormalize(value, "ModuleDefinitionFile", logger) : null, supportedProjectConfigurations, logger);
        ProjectReferences = project.ProjectReferences.Select(path => new CMakeProjectReference { Path = path }).ToArray();
        IsWin32Executable = project.LinkerSubsystem == "Windows";
        PrecompiledHeaderFile = new CMakeConfigDependentSetting(project.PrecompiledHeaderFile, supportedProjectConfigurations, logger)
            .Map((file, mode) => mode?.Value == "Use" && file != null ? TranslateAndNormalize(file, "PrecompiledHeaderFile", logger) : null, project.PrecompiledHeader, supportedProjectConfigurations, logger);
        Properties = [];

        ApplyTargetName(project, logger);
        ApplyLanguageStandards(project);
        ApplyAllProjectIncludesArePublic(project, logger);
        ApplyRuntimeLibrary(project, logger);
        ApplyMfcSupport(project, logger);
        ApplyCharacterSetSetting(project, logger);
        ApplyDisableSpecificWarnings(project, logger);
        ApplyTreatSpecificWarningsAsErrors(project, logger);
        ApplyTreatWarningAsError(project, logger);
        ApplyWarningLevel(project, logger);
        ApplyExternalWarningLevel(project, logger);
        ApplyTreatAngleIncludeAsExternal(project, logger);
        ApplyOpenMPSupport(project, logger);
        ApplyQt(project, qtVersion);
        ApplyConanPackages(project, conanPackageInfoRepository);
    }

    static CMakeExpression TranslateMSBuildMacros(CMakeExpression value, string settingName, ILogger logger)
    {
        string translatedValue = value.Value;
        translatedValue = Regex.Replace(translatedValue, @"\\\$\(Configuration(Name)?\)", "${CMAKE_BUILD_TYPE}", RegexOptions.IgnoreCase);
        translatedValue = Regex.Replace(translatedValue, @"\\\$\(ProjectDir\)[/\\]*", "${CMAKE_CURRENT_SOURCE_DIR}/", RegexOptions.IgnoreCase);
        translatedValue = Regex.Replace(translatedValue, @"\\\$\(ProjectName\)", "${PROJECT_NAME}", RegexOptions.IgnoreCase);
        translatedValue = Regex.Replace(translatedValue, @"\\\$\(SolutionDir\)[/\\]*", "${CMAKE_SOURCE_DIR}/", RegexOptions.IgnoreCase);
        translatedValue = Regex.Replace(translatedValue, @"\\\$\(SolutionName\)", "${CMAKE_PROJECT_NAME}", RegexOptions.IgnoreCase);

        var unsupportedMacros = Regex.Matches(translatedValue, @"\\\$\(([A-Za-z0-9_]+)\)");
        if (unsupportedMacros.Count > 0)
        {
            var unsupportedMacroNames = unsupportedMacros.Select(match => match.Groups[1].Value);
            logger.LogWarning($"Setting {settingName} with value \"{value.Value}\" contains unsupported MSBuild macros/properties: {string.Join(", ", unsupportedMacroNames)}");

            translatedValue = Regex.Replace(translatedValue, @"\\\$\(([A-Za-z0-9_]+)\)", "${$1}");
        }

        return CMakeExpression.Expression(translatedValue);
    }

    static CMakeExpression TranslateAndNormalize(CMakeExpression path, string settingName, ILogger logger)
    {
        return CMakeExpression.Expression(PathUtils.NormalizePath(TranslateMSBuildMacros(path, settingName, logger).Value));
    }

    static MSBuildProjectConfig[] FilterSupportedProjectConfigurations(IEnumerable<MSBuildProjectConfig> projectConfigurations, ILogger logger)
    {
        List<MSBuildProjectConfig> supportedProjectConfigurations = [];

        foreach (var projectConfig in projectConfigurations)
        {
            if (Config.IsMSBuildProjectConfigSupported(projectConfig))
                supportedProjectConfigurations.Add(projectConfig);
            else
                logger.LogWarning($"Skipping unsupported project configuration: {projectConfig}");
        }

        return supportedProjectConfigurations.ToArray();
    }

    static CMakeTargetType DetermineTargetType(MSBuildProject project)
    {
        var isHeaderOnlyLibrary = project.SourceFiles.Length == 0 && project.HeaderFiles.Length > 0;

        if (isHeaderOnlyLibrary)
            return CMakeTargetType.InterfaceLibrary;
        else
            return project.ConfigurationType switch
            {
                "Application" => CMakeTargetType.Executable,
                "StaticLibrary" => CMakeTargetType.StaticLibrary,
                "DynamicLibrary" => CMakeTargetType.SharedLibrary,
                _ => throw new CatastrophicFailureException($"ConfigurationType property is unsupported: {project.ConfigurationType}")
            };
    }

    static string[] DetectLanguages(IEnumerable<string> sourceFiles, ILogger logger)
    {
        List<string> result = [];

        if (sourceFiles.Any(file => file.EndsWith(".c", StringComparison.OrdinalIgnoreCase)))
            result.Add("C");
        if (sourceFiles.Any(file => Regex.IsMatch(file, @"\.(cpp|cxx|c\+\+|cc)$", RegexOptions.IgnoreCase)))
            result.Add("CXX");

        if (result.Count == 0)
            logger.LogWarning("Could not detect languages for project");

        return result.ToArray();
    }

    void ApplyTargetName(MSBuildProject project, ILogger logger)
    {
        if (project.TargetName.Values.Count == 0)
            return;

        var targetName = new CMakeConfigDependentSetting(project.TargetName, ProjectConfigurations, logger)
            .Map(value => value != null ? TranslateMSBuildMacros(value, "TargetName", logger) : null, ProjectConfigurations, logger)
            .ToCMakeExpression();

        if (targetName != CMakeExpression.Literal(project.ProjectName))
        {
            Properties["OUTPUT_NAME"] = targetName;
            OutputName = targetName;
        }
    }

    static MSBuildConfigDependentSetting<string[]> MergeIncludeDirectories(
        MSBuildProject project,
        IEnumerable<MSBuildProjectConfig> projectConfigurations)
    {
        var defaultValue = project.AdditionalIncludeDirectories.DefaultValue
            .Concat(project.IncludePath.DefaultValue)
            .Distinct()
            .ToArray();

        Dictionary<MSBuildProjectConfig, string[]> values = [];
        foreach (var projectConfig in projectConfigurations)        
            values[projectConfig] = project.AdditionalIncludeDirectories.GetEffectiveValue(projectConfig)
                .Concat(project.IncludePath.GetEffectiveValue(projectConfig))
                .Distinct()
                .ToArray();        

        return new("AdditionalIncludeDirectories+IncludePath", defaultValue, values);
    }

    static MSBuildConfigDependentSetting<string[]> MergeLibraryDirectories(
        MSBuildProject project,
        IEnumerable<MSBuildProjectConfig> projectConfigurations)
    {
        var defaultValue = project.AdditionalLibraryDirectories.DefaultValue
            .Concat(project.LibraryPath.DefaultValue)
            .Distinct()
            .ToArray();

        Dictionary<MSBuildProjectConfig, string[]> values = [];
        foreach (var projectConfig in projectConfigurations)        
            values[projectConfig] = project.AdditionalLibraryDirectories.GetEffectiveValue(projectConfig)
                .Concat(project.LibraryPath.GetEffectiveValue(projectConfig))
                .Distinct()
                .ToArray();

        return new("AdditionalLibraryDirectories+LibraryPath", defaultValue, values);
    }

    void ApplyLanguageStandards(MSBuildProject project)
    {
        var cppFeature = project.LanguageStandard switch
            {
                "stdcpplatest" => "cxx_std_23",
                "stdcpp23" => "cxx_std_23",
                "stdcpp20" => "cxx_std_20",
                "stdcpp17" => "cxx_std_17",
                "stdcpp14" => "cxx_std_14",
                "stdcpp11" => "cxx_std_11",
                "Default" or null or "" => null,
                _ => throw new CatastrophicFailureException($"Unsupported C++ language standard: {project.LanguageStandard}")
            };

        var cFeature = project.LanguageStandardC switch
            {
                "stdclatest" => "c_std_23",
                "stdc23" => "c_std_23",
                "stdc17" => "c_std_17",
                "stdc11" => "c_std_11",
                "Default" or null or "" => null,
                _ => throw new CatastrophicFailureException($"Unsupported C language standard: {project.LanguageStandardC}")
            };        

        if (cppFeature != null)
            CompileFeatures.AppendValue(Config.CommonConfig, CMakeExpression.Literal(cppFeature));

        if (cFeature != null)
            CompileFeatures.AppendValue(Config.CommonConfig, CMakeExpression.Literal(cFeature));
    }

    void ApplyRuntimeLibrary(MSBuildProject project, ILogger logger)
    {
        var msvcRuntimeLibrary = new CMakeConfigDependentSetting(project.RuntimeLibrary, ProjectConfigurations, logger).ToCMakeExpression();

        // if the setting has its default value, we prefer to not set it at all
        if (msvcRuntimeLibrary.Value == "$<$<CONFIG:Debug>:MultiThreadedDebugDLL>$<$<CONFIG:Release>:MultiThreadedDLL>")
            return;

        // for the common case of MultiThreadedDebug for debug and MultiThreaded for release, replace the CMake expression with a simpler, equivalent one
        if (msvcRuntimeLibrary.Value == "$<$<CONFIG:Debug>:MultiThreadedDebug>$<$<CONFIG:Release>:MultiThreaded>")
            msvcRuntimeLibrary = CMakeExpression.Expression("MultiThreaded$<$<CONFIG:Debug>:Debug>");

        Properties["MSVC_RUNTIME_LIBRARY"] = msvcRuntimeLibrary;
    }

    void ApplyCharacterSetSetting(MSBuildProject project, ILogger logger)
    {
        Defines = Defines.Map((defines, charSet) => (charSet?.Value) switch
        {
            "Unicode" => AppendDefineIfNotPresent(defines, CMakeExpression.Literal("UNICODE"), CMakeExpression.Literal("_UNICODE")),
            "MultiByte" => AppendDefineIfNotPresent(defines, CMakeExpression.Literal("_MBCS")),
            "NotSet" or "" or null => defines,
            _ => throw new CatastrophicFailureException($"Invalid value for CharacterSet: {charSet}")
        }, project.CharacterSet, ProjectConfigurations, logger);
    }

    void ApplyMfcSupport(MSBuildProject project, ILogger logger)
    {
        static CMakeExpression TranslateMfcFlag(CMakeExpression? useOfMfc) => 
            useOfMfc?.Value.ToLowerInvariant() switch
            {
                "false" or "" or null => CMakeExpression.Literal("0"),
                "static" => CMakeExpression.Literal("1"),
                "dynamic" => CMakeExpression.Literal("2"),
                _ => throw new CatastrophicFailureException($"Invalid value for UseOfMfc: {useOfMfc}")
            };        

        var mfcFlag = new CMakeConfigDependentSetting(project.UseOfMfc, ProjectConfigurations, logger)
            .Map(value => TranslateMfcFlag(value), ProjectConfigurations, logger)
            .ToCMakeExpression();

        if (mfcFlag.Value == "0")
            return;

        FindPackages.Add(new CMakeFindPackage("MFC", Required: true));

        Properties["CMAKE_MFC_FLAG"] = mfcFlag;

        Defines = Defines.Map((defines, useOfMfc) =>
        {
            if (TranslateMfcFlag(useOfMfc).Value != "0")
                return AppendDefineIfNotPresent(defines, CMakeExpression.Literal("_AFXDLL"));
            else
                return defines;
        }, project.UseOfMfc, ProjectConfigurations, logger);
    }

    void ApplyDisableSpecificWarnings(MSBuildProject project, ILogger logger)
    {
        Options = Options.Map(
            (options, warnings) => [.. options, .. warnings.Select(w => Convert.ToInt32(w.Value)).Select(w => CMakeExpression.Literal($"/wd{w}"))],
            project.DisableSpecificWarnings, ProjectConfigurations, logger);
    }

    void ApplyTreatSpecificWarningsAsErrors(MSBuildProject project, ILogger logger)
    {
        Options = Options.Map(
            (options, warnings) => [.. options, .. warnings.Select(w => Convert.ToInt32(w.Value)).Select(w => CMakeExpression.Literal($"/we{w}"))],
            project.TreatSpecificWarningsAsErrors, ProjectConfigurations, logger);
    }

    void ApplyTreatWarningAsError(MSBuildProject project, ILogger logger)
    {
        var compileWarningAsError = new CMakeConfigDependentSetting(project.TreatWarningAsError, ProjectConfigurations, logger)
            .Map(value => (value?.Value.ToLowerInvariant()) switch
            {
                "true" => CMakeExpression.Literal("ON"),
                "false" or "" or null => CMakeExpression.Literal("OFF"),
                _ => throw new CatastrophicFailureException($"Invalid value for TreatWarningAsError: {value?.Value}")
            }, ProjectConfigurations, logger)
            .ToCMakeExpression();

        // if the setting has its default value, we prefer to not set it at all
        if (compileWarningAsError.Value == "OFF")
            return;

        Properties["COMPILE_WARNING_AS_ERROR"] = compileWarningAsError;
    }

    void ApplyWarningLevel(MSBuildProject project, ILogger logger)
    {
        Options = Options.Map((options, level) => level switch
        {
            CMakeExpression e when e.Value == "TurnOffAllWarnings" => [.. options, CMakeExpression.Literal("/W0")],
            CMakeExpression e when e.Value == "Level1" => [.. options, CMakeExpression.Literal("/W1")],
            CMakeExpression e when e.Value == "Level2" => [.. options, CMakeExpression.Literal("/W2")],
            CMakeExpression e when e.Value == "Level3" => [.. options, CMakeExpression.Literal("/W3")],
            CMakeExpression e when e.Value == "Level4" => [.. options, CMakeExpression.Literal("/W4")],
            null or { Value: "" } => options,
            _ => throw new CatastrophicFailureException($"Invalid value for WarningLevel: {level?.Value}")
        }, project.WarningLevel, ProjectConfigurations, logger);
    }

    void ApplyExternalWarningLevel(MSBuildProject project, ILogger logger)
    {
        Options = Options.Map((options, level) => level switch
        {
            CMakeExpression e when e.Value == "TurnOffAllWarnings" => [.. options, CMakeExpression.Literal("/external:W0")],
            CMakeExpression e when e.Value == "Level1" => [.. options, CMakeExpression.Literal("/external:W1")],
            CMakeExpression e when e.Value == "Level2" => [.. options, CMakeExpression.Literal("/external:W2")],
            CMakeExpression e when e.Value == "Level3" => [.. options, CMakeExpression.Literal("/external:W3")],
            CMakeExpression e when e.Value == "Level4" => [.. options, CMakeExpression.Literal("/external:W4")],
            null or { Value: "" } => options,
            _ => throw new CatastrophicFailureException($"Invalid value for ExternalWarningLevel: {level?.Value}")
        }, project.ExternalWarningLevel, ProjectConfigurations, logger);
    }

    void ApplyTreatAngleIncludeAsExternal(MSBuildProject project, ILogger logger)
    {
        Options = Options.Map((options, treatAsExternal) => (treatAsExternal?.Value.ToLowerInvariant()) switch
        {
            "true" => [.. options, CMakeExpression.Literal("/external:anglebrackets")],
            "false" or "" or null => options,
            _ => throw new CatastrophicFailureException($"Invalid value for TreatAngleIncludeAsExternal: {treatAsExternal}"),
        }, project.TreatAngleIncludeAsExternal, ProjectConfigurations, logger);
    }

    void ApplyAllProjectIncludesArePublic(MSBuildProject project, ILogger logger)
    {
        PublicIncludePaths = PublicIncludePaths.Map((directories, allArePublic) => (allArePublic?.Value.ToLowerInvariant()) switch
        {
            "true" => [.. directories, CMakeExpression.Expression("${CMAKE_CURRENT_SOURCE_DIR}")],
            "false" or "" or null => directories,
            _ => throw new CatastrophicFailureException($"Invalid value for AllProjectIncludesArePublic: {allArePublic}"),
        }, project.AllProjectIncludesArePublic, ProjectConfigurations, logger);
    }

    void ApplyOpenMPSupport(MSBuildProject project, ILogger logger)
    {
        var usesOpenMP = project.OpenMPSupport.Values.Values.Contains("true", StringComparer.OrdinalIgnoreCase);
        if (usesOpenMP)
        {
            FindPackages.Add(new CMakeFindPackage("OpenMP", Required: true));

            Libraries = Libraries.Map((libs, openMP) => (openMP?.Value.ToLowerInvariant()) switch
            {
                "true" => [.. libs, CMakeExpression.Literal("OpenMP::OpenMP_CXX")],
                "false" or "" or null => libs,
                _ => throw new CatastrophicFailureException($"Invalid value for OpenMPSupport: {openMP}"),
            }, project.OpenMPSupport, ProjectConfigurations, logger);
        }
    }

    void ApplyQt(MSBuildProject project, int? qtVersion)
    {
        if (project.QtModules.Length == 0)
            return;
                
        if (qtVersion == null)
            throw new CatastrophicFailureException("Project uses Qt but no Qt version is set. Specify the version with --qt-version.");

        var qtModules =
            project.QtModules
            .Select(module => QtModuleInfoRepository.GetQtModuleInfo(module, qtVersion!.Value))
            .OrderBy(m => m.CMakeTargetName);

        var qtComponents = qtModules.Select(m => m.CMakeComponentName).ToArray();
        FindPackages.Add(new CMakeFindPackage($"Qt{qtVersion}", Required: true, Components: qtComponents));

        foreach (var module in qtModules)        
            Libraries.AppendValue(Config.CommonConfig, CMakeExpression.Literal(module.CMakeTargetName));

        if (project.RequiresQtMoc)
            Properties.Add("AUTOMOC", CMakeExpression.Literal("ON"));
        if (project.RequiresQtUic)
            Properties.Add("AUTOUIC", CMakeExpression.Literal("ON"));
        if (project.RequiresQtRcc)
            Properties.Add("AUTORCC", CMakeExpression.Literal("ON"));
    }

    void ApplyConanPackages(MSBuildProject project, ConanPackageInfoRepository conanPackageInfoRepository)
    {
        var conanPackages =
            project.ConanPackages
            .Select(packageName => conanPackageInfoRepository.GetConanPackageInfo(packageName!))
            .OrderBy(p => p.CMakeTargetName);

        foreach (var package in conanPackages)
        {
            FindPackages.Add(new CMakeFindPackage(package.CMakeConfigName, Required: true, Config: true));
            Libraries.AppendValue(Config.CommonConfig, CMakeExpression.Literal(package.CMakeTargetName));
        }
    }

    static CMakeExpression[] AppendDefineIfNotPresent(CMakeExpression[] defines, params CMakeExpression[] additionalDefines)
    {
        return [.. defines, .. additionalDefines.Except(defines)];
    }

    public ISet<CMakeProject> GetAllReferencedProjects()
    {
        var referencedProjects = new HashSet<CMakeProject>();

        void GetAllReferencedProjectsInner(CMakeProject project)
        {
            foreach (var projectReference in project.ProjectReferences)
                if (referencedProjects.Add(projectReference.Project!))
                    GetAllReferencedProjectsInner(projectReference.Project!);
        }

        GetAllReferencedProjectsInner(this);

        return referencedProjects;
    }
}

enum CMakeTargetType
{
    Executable,
    StaticLibrary,
    SharedLibrary,
    InterfaceLibrary
}

record CMakeFindPackage(string PackageName, bool Required = false, bool Config = false, string[]? Components = null);

class CMakeProjectReference
{
    public required string Path { get; set; }
    public CMakeProject? Project { get; set; }
}
