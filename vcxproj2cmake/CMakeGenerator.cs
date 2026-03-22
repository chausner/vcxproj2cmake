using Microsoft.Extensions.Logging;
using Scriban;
using Scriban.Runtime;
using System.IO.Abstractions;
using System.Collections;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace vcxproj2cmake;

class CMakeGenerator
{
    readonly IFileSystem fileSystem;
    readonly ILogger logger;

    public CMakeGenerator(IFileSystem fileSystem, ILogger logger)
    {
        this.fileSystem = fileSystem;
        this.logger = logger;
    }

    public void Generate(CMakeSolution? solution, IEnumerable<CMakeProject> projects, CMakeGeneratorSettings settings)
    {
        var projectCMakeListsTemplate = LoadTemplate("vcxproj2cmake.Resources.Templates.Project-CMakeLists.txt.scriban");
        var solutionCMakeListsTemplate = LoadTemplate("vcxproj2cmake.Resources.Templates.Solution-CMakeLists.txt.scriban");

        ValidateFolders(solution, projects);

        foreach (var project in projects)
            GenerateCMakeForProject(project, projects, projectCMakeListsTemplate, settings);

        if (solution != null)
            GenerateCMakeForSolution(solution, projects, solutionCMakeListsTemplate, settings);
    }

    static void ValidateFolders(CMakeSolution? solution, IEnumerable<CMakeProject> projects)
    {
        HashSet<string> folders = [];

        foreach (var project in projects)
        {
            var folder = Path.GetDirectoryName(project.AbsoluteProjectPath)!;
            if (!folders.Add(folder))
                throw new CatastrophicFailureException($"Directory {folder} contains two or more projects. This is not supported.");
        }

        if (solution != null && !folders.Add(Path.GetDirectoryName(solution.AbsoluteSolutionPath)!))
            throw new CatastrophicFailureException($"The solution file and at least one project file are located in the same directory. This is not supported.");
    }

    void GenerateCMake(object model, IEnumerable<CMakeProject> allProjects, string destinationPath, Template cmakeListsTemplate, CMakeGeneratorSettings settings)
    {
        logger.LogInformation($"Generating {destinationPath}");

        var scriptObject = CreateTemplateModel(model, settings, allProjects);

        var context = new TemplateContext();
        context.LoopLimit = 0;
        context.RecursiveLimit = 0;
        context.PushGlobal(scriptObject);
        var result = cmakeListsTemplate.Render(context);

        if (settings.IndentStyle != IndentStyle.Spaces || settings.IndentSize != 4)
            result = ApplyIndentation(result, settings.IndentStyle, settings.IndentSize);

        if (settings.DryRun)
        {
            var newline = Environment.NewLine;
            var extraIndentedResult = Regex.Replace(result, "^", "    ", RegexOptions.Multiline);
            logger.LogInformation($"Generated output for {destinationPath}:{newline}{newline}{extraIndentedResult}");
        }
        else
        {
            fileSystem.File.WriteAllText(destinationPath, result);
        }
    }

    static string ApplyIndentation(string text, IndentStyle indentStyle, int indentSize)
    {
        return Regex.Replace(text, @"^((    )+)", match =>
        {
            var indentLevel = match.Length / 4;
            return indentStyle switch
            {
                IndentStyle.Spaces => new string(' ', indentLevel * indentSize),
                IndentStyle.Tabs => new string('\t', indentLevel),
                _ => throw new ArgumentException($"Invalid indent style: {indentStyle}")
            };
        }, RegexOptions.Multiline);
    }

    void GenerateCMakeForProject(CMakeProject project, IEnumerable<CMakeProject> allProjects, Template cmakeListsTemplate, CMakeGeneratorSettings settings)
    {
        string cmakeListsPath = Path.Combine(Path.GetDirectoryName(project.AbsoluteProjectPath)!, "CMakeLists.txt");

        using var scope = logger.BeginScope(Path.GetFileName(project.AbsoluteProjectPath));

        GenerateCMake(project, allProjects, cmakeListsPath, cmakeListsTemplate, settings);
    }

    void GenerateCMakeForSolution(CMakeSolution solution, IEnumerable<CMakeProject> allProjects, Template cmakeListsTemplate, CMakeGeneratorSettings settings)
    {
        string cmakeListsPath = Path.Combine(Path.GetDirectoryName(solution.AbsoluteSolutionPath)!, "CMakeLists.txt");

        using var scope = logger.BeginScope(Path.GetFileName(solution.AbsoluteSolutionPath));

        GenerateCMake(solution, allProjects, cmakeListsPath, cmakeListsTemplate, settings);
    }

    static Template LoadTemplate(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var streamReader = new StreamReader(stream);
        string content = streamReader.ReadToEnd();
        return Template.Parse(content);
    }

    ScriptObject CreateTemplateModel(object model, CMakeGeneratorSettings settings, IEnumerable<CMakeProject> allProjects)
    {
        var scriptObject = model switch
        {
            CMakeProject project => CreateProjectScriptObject(project),
            CMakeSolution solution => CreateSolutionScriptObject(solution),
            _ => throw new ArgumentException($"Unsupported template model type: {model.GetType().FullName}", nameof(model))
        };

        scriptObject.Add("enable_standalone_project_builds", settings.EnableStandaloneProjectBuilds);
        scriptObject.Add("indent_style", settings.IndentStyle.ToString());
        scriptObject.Add("indent_size", settings.IndentSize);
        scriptObject.Add("dry_run", settings.DryRun);
        scriptObject.Add("fail", DelegateCustomFunction.Create<string>(error => throw new CatastrophicFailureException(error)));
        scriptObject.Add("literal", DelegateCustomFunction.CreateFunc<string, string>(s => ToCMakeLiteral(s)));
        scriptObject.Add("unquoted_literal", DelegateCustomFunction.CreateFunc<string, string>(s => ToCMakeLiteral(s, unquoted: true)));
        scriptObject.Add("normalize_path", DelegateCustomFunction.CreateFunc<string, string>(PathUtils.NormalizePath));
        scriptObject.Add("get_config_expression", DelegateCustomFunction.CreateFunc<Config, string, string>((config, value) => config.Apply(CMakeExpression.Expression(value)).ToString()));
        scriptObject.Add("order_project_references_by_dependencies", DelegateCustomFunction.CreateFunc<IEnumerable, ScriptArray>(projectReferences => OrderProjectReferencesByDependencies(projectReferences, allProjects)));
        scriptObject.Add("get_directory_name", DelegateCustomFunction.CreateFunc<string?, string?>(Path.GetDirectoryName));
        scriptObject.Add("get_relative_path", DelegateCustomFunction.CreateFunc<string, string, string?>((path, relativeTo) => Path.GetRelativePath(relativeTo, path)));
        scriptObject.Add("prepend_relative_paths_with_cmake_current_source_dir", DelegateCustomFunction.CreateFunc<string, string>(PrependRelativePathsWithCMakeCurrentSourceDir));
        return scriptObject;
    }

    static ScriptObject CreateProjectScriptObject(CMakeProject project)
    {
        var scriptObject = new ScriptObject
        {
            ["absolute_project_path"] = project.AbsoluteProjectPath,
            ["project_name"] = project.ProjectName,
            ["languages"] = ToScriptArray(project.Languages.Cast<object?>()),
            ["target_type"] = project.TargetType.ToString(),
            ["find_packages"] = ToScriptArray(project.FindPackages.Select(CreateFindPackageScriptObject)),
            ["compile_features"] = CreateConfigDependentMultiSettingScriptObject(project.CompileFeatures),
            ["source_files"] = ToScriptArray(project.SourceFiles.Select(expression => expression.ToString())),
            ["properties"] = CreateExpressionDictionaryScriptArray(project.Properties),
            ["module_definition_file"] = CreateConfigDependentSettingScriptObject(project.ModuleDefinitionFile),
            ["project_references"] = ToScriptArray(project.ProjectReferences.Select(CreateProjectReferenceScriptObject)),
            ["is_win32_executable"] = project.IsWin32Executable,
            ["precompiled_header_file"] = CreateConfigDependentSettingScriptObject(project.PrecompiledHeaderFile),
            ["public_include_paths"] = CreateConfigDependentMultiSettingScriptObject(project.PublicIncludePaths),
            ["include_paths"] = CreateConfigDependentMultiSettingScriptObject(project.IncludePaths),
            ["defines"] = CreateConfigDependentMultiSettingScriptObject(project.Defines),
            ["linker_paths"] = CreateConfigDependentMultiSettingScriptObject(project.LinkerPaths),
            ["libraries"] = CreateConfigDependentMultiSettingScriptObject(project.Libraries),
            ["options"] = CreateConfigDependentMultiSettingScriptObject(project.Options)
        };

        return scriptObject;
    }

    static ScriptObject CreateSolutionScriptObject(CMakeSolution solution)
    {
        return new ScriptObject
        {
            ["absolute_solution_path"] = solution.AbsoluteSolutionPath,
            ["solution_name"] = solution.SolutionName,
            ["projects"] = ToScriptArray(solution.Projects.Select(CreateProjectReferenceScriptObject)),
            ["solution_is_top_level"] = solution.SolutionIsTopLevel
        };
    }

    static ScriptObject CreateFindPackageScriptObject(CMakeFindPackage package)
    {
        return new ScriptObject
        {
            ["package_name"] = package.PackageName,
            ["required"] = package.Required,
            ["config"] = package.Config,
            ["components"] = package.Components != null ? ToScriptArray(package.Components.Cast<object?>()) : null
        };
    }

    static ScriptObject CreateProjectReferenceScriptObject(CMakeProjectReference projectReference)
    {
        var scriptObject = new ScriptObject
        {
            ["path"] = projectReference.Path
        };

        if (projectReference.Project != null)
        {
            scriptObject["project"] = new ScriptObject
            {
                ["project_name"] = projectReference.Project.ProjectName,
                ["absolute_project_path"] = projectReference.Project.AbsoluteProjectPath
            };
        }

        return scriptObject;
    }

    static ScriptObject CreateConfigDependentSettingScriptObject(CMakeConfigDependentSetting setting)
    {
        return new ScriptObject
        {
            ["is_empty"] = setting.IsEmpty,
            ["entries"] = CreateSettingValuesScriptArray(setting.Values.Select(kvp => CreateSettingEntryScriptObject(kvp.Key, kvp.Value.Value)))
        };
    }

    static ScriptObject CreateConfigDependentMultiSettingScriptObject(CMakeConfigDependentMultiSetting setting)
    {
        return new ScriptObject
        {
            ["is_empty"] = setting.IsEmpty,
            ["entries"] = CreateSettingValuesScriptArray(setting.Values.Select(kvp => CreateSettingEntryScriptObject(kvp.Key, ToScriptArray(kvp.Value.Select(value => value.Value)))))
        };
    }

    static ScriptArray CreateExpressionDictionaryScriptArray(IEnumerable<KeyValuePair<string, CMakeExpression>> values)
    {
        return ToScriptArray(values.Select(kvp => new ScriptObject
        {
            ["key"] = kvp.Key,
            ["value"] = kvp.Value.ToString()
        }));
    }

    static ScriptArray CreateSettingValuesScriptArray(IEnumerable<ScriptObject> values)
    {
        return ToScriptArray(values);
    }

    static ScriptObject CreateSettingEntryScriptObject(Config config, object value)
    {
        return new ScriptObject
        {
            ["key"] = config,
            ["value"] = value
        };
    }

    static ScriptArray OrderProjectReferencesByDependencies(IEnumerable projectReferences, IEnumerable<CMakeProject> allProjects)
    {
        var projectReferencesArray = projectReferences.Cast<object?>().ToArray();
        var orderedProjects = ProjectDependencyUtils.OrderProjectsByDependencies(allProjects);
        var projectOrder = orderedProjects
            .Select((project, index) => new { project.AbsoluteProjectPath, index })
            .ToDictionary(entry => entry.AbsoluteProjectPath, entry => entry.index, StringComparer.OrdinalIgnoreCase);

        return ToScriptArray(projectReferencesArray.OrderBy(projectReference => GetProjectReferenceOrder(projectReference, projectOrder)));
    }

    static int GetProjectReferenceOrder(object? projectReference, IReadOnlyDictionary<string, int> projectOrder)
    {
        if (projectReference is not ScriptObject projectReferenceScriptObject)
            throw new ArgumentException("Expected Scriban project reference to be a ScriptObject.", nameof(projectReference));

        if (!projectReferenceScriptObject.TryGetValue("project", out var project) || project is not ScriptObject projectScriptObject)
            throw new CatastrophicFailureException("Project reference is missing resolved project metadata.");

        if (!projectScriptObject.TryGetValue("absolute_project_path", out var absoluteProjectPathValue) || absoluteProjectPathValue is not string absoluteProjectPath)
            throw new CatastrophicFailureException("Resolved project metadata is missing absolute_project_path.");

        if (!projectOrder.TryGetValue(absoluteProjectPath, out var order))
            throw new CatastrophicFailureException($"Project dependency order does not contain {absoluteProjectPath}.");

        return order;
    }

    static ScriptArray ToScriptArray(IEnumerable values)
    {
        var array = new ScriptArray();
        foreach (var value in values)
            array.Add(value);

        return array;
    }

    static string ToCMakeLiteral(string value, bool unquoted = false)
    {
        bool NeedsQuoting(char c) =>
            char.IsWhiteSpace(c) ||  // space, tab, newline …
            c == ';' ||              // list separator inside variables
            c == '#' ||              // comment introducer
            c == '(' || c == ')' ||  // command delimiters
            c == '"' || c == '\\' || // must be escaped inside quotes
            c == '$';                // variable expansion

        bool mustQuote = value.Length == 0 || value.Any(NeedsQuoting);
        if (!mustQuote)
            return value;

        var sb = new StringBuilder(value.Length + 8);

        if (!unquoted)
            sb.Append('"');

        foreach (char c in value)
            sb.Append(c switch
            {
                '\\' => "\\\\", // backslash
                '\"' => "\\\"", // quote
                '\n' => "\\n",  // newline
                '\r' => "\\r",  // carriage return
                '\t' => "\\t",  // tab
                '$' => "\\$",   // prevent ${VAR} expansion
                _ => c.ToString()
            });  

        if (!unquoted)
            sb.Append('"');

        return sb.ToString();
    }

    static string PrependRelativePathsWithCMakeCurrentSourceDir(string normalizedPath)
    {
        var isAbsolutePath = Path.IsPathRooted(normalizedPath);

        // if a path starts with a CMake variable, we just assume that the variable resolves to an absolute path
        isAbsolutePath |= normalizedPath.StartsWith("${");

        if (!isAbsolutePath)
            if (normalizedPath == ".")
                return "${CMAKE_CURRENT_SOURCE_DIR}";
            else
                return "${CMAKE_CURRENT_SOURCE_DIR}/" + normalizedPath;
        else
            return normalizedPath;
    }
}

record CMakeGeneratorSettings(bool EnableStandaloneProjectBuilds, IndentStyle IndentStyle, int IndentSize, bool DryRun);

public enum IndentStyle
{
    Spaces,
    Tabs
}
