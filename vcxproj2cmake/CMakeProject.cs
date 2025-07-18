using Microsoft.Extensions.Logging;
using System.IO.Abstractions;
using System.Text.RegularExpressions;

namespace vcxproj2cmake;

class CMakeProject
{
    public MSBuildProject MSBuildProject { get; }
    public string AbsoluteProjectPath { get; set; }
    public string ProjectName { get; set; }
    public string[] ProjectConfigurations { get; set; }
    public string[] Languages { get; set; }
    public string ConfigurationType { get; set; }
    public IList<CMakeFindPackage> FindPackages { get; set; }
    public CMakeConfigDependentMultiSetting CompileFeatures { get; set; }
    public string[] SourceFiles { get; set; }
    public string OutputName { get; set; }
    public CMakeConfigDependentMultiSetting IncludePaths { get; set; }
    public CMakeConfigDependentMultiSetting PublicIncludePaths { get; set; }
    public CMakeConfigDependentMultiSetting LinkerPaths { get; set; }
    public CMakeConfigDependentMultiSetting Libraries { get; set; }
    public CMakeConfigDependentMultiSetting Defines { get; set; }
    public CMakeConfigDependentMultiSetting Options { get; set; }
    public OrderedDictionary<string, string> Properties { get; set; }
    public CMakeProjectReference[] ProjectReferences { get; set; }
    public bool IsWin32Executable { get; set; }
    public bool IsHeaderOnlyLibrary { get; set; }
    public CMakeConfigDependentSetting PrecompiledHeaderFile { get; set; }

    public CMakeProject(MSBuildProject project, int? qtVersion, ConanPackageInfoRepository conanPackageInfoRepository, IFileSystem fileSystem, ILogger logger)
    {
        var supportedProjectConfigurations = FilterSupportedProjectConfigurations(project.ProjectConfigurations, logger);

        MSBuildProject = project;
        AbsoluteProjectPath = project.AbsoluteProjectPath;
        ProjectName = project.ProjectName;
        ProjectConfigurations = supportedProjectConfigurations;
        Languages = DetectLanguages(project.SourceFiles, logger);
        ConfigurationType = project.ConfigurationType;
        FindPackages = [];
        CompileFeatures = new("CompileFeatures", []);
        SourceFiles = project.SourceFiles;
        OutputName = project.ProjectName;  // may get overridden in ApplyTargetName
        IncludePaths = new(project.AdditionalIncludeDirectories, supportedProjectConfigurations, logger);
        PublicIncludePaths = new(project.PublicIncludeDirectories, supportedProjectConfigurations, logger);
        LinkerPaths = new(project.AdditionalLibraryDirectories, supportedProjectConfigurations, logger);
        Libraries = new(project.AdditionalDependencies, supportedProjectConfigurations, logger);
        Defines = new(project.PreprocessorDefinitions, supportedProjectConfigurations, logger);
        Options = new(project.AdditionalOptions, supportedProjectConfigurations, logger);
        ProjectReferences = project.ProjectReferences.Select(path => new CMakeProjectReference { Path = path }).ToArray();
        IsWin32Executable = project.LinkerSubsystem == "Windows";
        PrecompiledHeaderFile = new CMakeConfigDependentSetting(project.PrecompiledHeaderFile, supportedProjectConfigurations, logger)
            .Map((file, mode) => mode == "Use" ? file : null, project.PrecompiledHeader, supportedProjectConfigurations, logger);
        Properties = [];

        // We don't rely on ConfigurationType to determine if the project is a header-only library
        // since there is no specific configuration type for header-only libraries in MSBuild.
        IsHeaderOnlyLibrary = project.SourceFiles.Length == 0 && project.HeaderFiles.Length > 0;

        ApplyTargetName(project);
        ApplyLanguageStandards(project);
        ApplyAllProjectIncludesArePublic(project, logger);
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

    static string[] FilterSupportedProjectConfigurations(IEnumerable<string> projectConfigurations, ILogger logger)
    {
        List<string> supportedProjectConfigurations = [];

        foreach (var projectConfig in projectConfigurations)
        {
            if (Config.IsMSBuildProjectConfigNameSupported(projectConfig))
                supportedProjectConfigurations.Add(projectConfig);
            else
                logger.LogWarning($"Skipping unsupported project configuration: {projectConfig}");
        }

        return supportedProjectConfigurations.ToArray();
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

    void ApplyTargetName(MSBuildProject project)
    {
        if (project.TargetName.Values.Count == 0)
            return;

        var targetName =
            project.TargetName.Values.Values
            .Distinct()
            .SingleWithException(() =>
                throw new CatastrophicFailureException(
                    "TargetName property is inconsistent between configurations"));

        if (targetName != project.ProjectName)
        {
            Properties["OUTPUT_NAME"] = targetName;
            OutputName = targetName;
        }
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
            CompileFeatures.AppendValue(Config.CommonConfig, cppFeature);

        if (cFeature != null)
            CompileFeatures.AppendValue(Config.CommonConfig, cFeature);
    }

    void ApplyCharacterSetSetting(MSBuildProject project, ILogger logger)
    {
        Defines = Defines.Map((defines, charSet) => charSet switch
        {
            "Unicode" => [.. defines, "UNICODE", "_UNICODE"],
            "MultiByte" => [.. defines, "_MBCS"],
            "NotSet" or "" or null => defines,
            _ => throw new CatastrophicFailureException($"Invalid value for CharacterSet: {charSet}")
        }, project.CharacterSet, ProjectConfigurations, logger);
    }

    void ApplyDisableSpecificWarnings(MSBuildProject project, ILogger logger)
    {
        Options = Options.Map(
            (options, warnings) => [.. options, .. warnings.Select(w => Convert.ToInt32(w)).Select(w => $"/wd{w}")],
            project.DisableSpecificWarnings, ProjectConfigurations, logger);
    }

    void ApplyTreatSpecificWarningsAsErrors(MSBuildProject project, ILogger logger)
    {
        Options = Options.Map(
            (options, warnings) => [.. options, .. warnings.Select(w => Convert.ToInt32(w)).Select(w => $"/we{w}")],
            project.TreatSpecificWarningsAsErrors, ProjectConfigurations, logger);
    }

    void ApplyTreatWarningAsError(MSBuildProject project, ILogger logger)
    {
        Options = Options.Map((options, treatAsError) => (treatAsError?.ToLowerInvariant()) switch
        {
            "true" => [.. options, "/WX"],
            "false" or "" or null => options,
            _ => throw new CatastrophicFailureException($"Invalid value for TreatWarningAsError: {treatAsError}"),
        }, project.TreatWarningAsError, ProjectConfigurations, logger);
    }

    void ApplyWarningLevel(MSBuildProject project, ILogger logger)
    {
        Options = Options.Map((options, level) => level switch
        {
            "TurnOffAllWarnings" => [.. options, "/W0"],
            "Level1" => [.. options, "/W1"],
            "Level2" => [.. options, "/W2"],
            "Level3" => [.. options, "/W3"],
            "Level4" => [.. options, "/W4"],
            "" or null => options,
            _ => throw new CatastrophicFailureException($"Invalid value for WarningLevel: {level}")
        }, project.WarningLevel, ProjectConfigurations, logger);
    }

    void ApplyExternalWarningLevel(MSBuildProject project, ILogger logger)
    {
        Options = Options.Map((options, level) => level switch
        {
            "TurnOffAllWarnings" => [.. options, "/external:W0"],
            "Level1" => [.. options, "/external:W1"],
            "Level2" => [.. options, "/external:W2"],
            "Level3" => [.. options, "/external:W3"],
            "Level4" => [.. options, "/external:W4"],
            "" or null => options,
            _ => throw new CatastrophicFailureException($"Invalid value for ExternalWarningLevel: {level}")
        }, project.ExternalWarningLevel, ProjectConfigurations, logger);
    }

    void ApplyTreatAngleIncludeAsExternal(MSBuildProject project, ILogger logger)
    {
        Options = Options.Map((options, treatAsExternal) => (treatAsExternal?.ToLowerInvariant()) switch
        {
            "true" => [.. options, "/external:anglebrackets"],
            "false" or "" or null => options,
            _ => throw new CatastrophicFailureException($"Invalid value for TreatAngleIncludeAsExternal: {treatAsExternal}"),
        }, project.TreatAngleIncludeAsExternal, ProjectConfigurations, logger);
    }

    void ApplyAllProjectIncludesArePublic(MSBuildProject project, ILogger logger)
    {
        PublicIncludePaths = PublicIncludePaths.Map((directories, allArePublic) => (allArePublic?.ToLowerInvariant()) switch
        {
            "true" => [.. directories, "$(ProjectDir)"],
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

            Libraries = Libraries.Map((libs, openMP) => (openMP?.ToLowerInvariant()) switch
            {
                "true" => [.. libs, "OpenMP::OpenMP_CXX"],
                "false" or "" or null => libs,
                _ => throw new CatastrophicFailureException($"Invalid value for OpenMPSupport: {openMP}"),
            }, project.OpenMPSupport, ProjectConfigurations, logger);
        }
    }

    private void ApplyQt(MSBuildProject project, int? qtVersion)
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
            Libraries.AppendValue(Config.CommonConfig, module.CMakeTargetName);

        if (project.RequiresQtMoc)
            Properties.Add("AUTOMOC", "ON");
        if (project.RequiresQtUic)
            Properties.Add("AUTOUIC", "ON");
        if (project.RequiresQtRcc)
            Properties.Add("AUTORCC", "ON");
    }

    private void ApplyConanPackages(MSBuildProject project, ConanPackageInfoRepository conanPackageInfoRepository)
    {
        var conanPackages =
            project.ConanPackages
            .Select(packageName => conanPackageInfoRepository.GetConanPackageInfo(packageName!))
            .OrderBy(p => p.CMakeTargetName);

        foreach (var package in conanPackages)
        {
            FindPackages.Add(new CMakeFindPackage(package.CMakeConfigName, Required: true, Config: true));
            Libraries.AppendValue(Config.CommonConfig, package.CMakeTargetName);
        }
    }

    public ISet<CMakeProject> GetAllReferencedProjects(IEnumerable<CMakeProject> allProjects)
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

record CMakeFindPackage(string PackageName, bool Required = false, bool Config = false, string[]? Components = null);

class CMakeProjectReference
{
    public required string Path { get; set; }
    public CMakeProject? Project { get; set; }
}
