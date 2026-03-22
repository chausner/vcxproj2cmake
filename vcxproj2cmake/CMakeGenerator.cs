using Microsoft.Extensions.Logging;
using Scriban;
using Scriban.Runtime;
using System.IO.Abstractions;
using System.Diagnostics.CodeAnalysis;
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

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Scriban reflection import is limited to known template model types whose public properties are preserved explicitly for trimming and Native AOT.")]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(CMakeProject))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(CMakeSolution))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(CMakeProjectReference))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(CMakeFindPackage))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(CMakeConfigDependentSetting))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(CMakeConfigDependentMultiSetting))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(CMakeExpression))]
    void GenerateCMake(object model, IEnumerable<CMakeProject> allProjects, string destinationPath, Template cmakeListsTemplate, CMakeGeneratorSettings settings)
    {
        logger.LogInformation($"Generating {destinationPath}");

        var scriptObject = new ScriptObject();
        scriptObject.Import(model);
        scriptObject.Import(settings);
        scriptObject.Add("fail", DelegateCustomFunction.Create<string>(error => throw new CatastrophicFailureException(error)));
        scriptObject.Add("literal", DelegateCustomFunction.CreateFunc<string, string>(s => ToCMakeLiteral(s)));
        scriptObject.Add("unquoted_literal", DelegateCustomFunction.CreateFunc<string, string>(s => ToCMakeLiteral(s, unquoted: true)));
        scriptObject.Add("normalize_path", DelegateCustomFunction.CreateFunc<string, string>(PathUtils.NormalizePath));
        scriptObject.Add("get_config_expression", DelegateCustomFunction.CreateFunc<Config, CMakeExpression, CMakeExpression>((config, value) => config.Apply(value)));
        scriptObject.Add("order_project_references_by_dependencies", DelegateCustomFunction.CreateFunc<IEnumerable<CMakeProjectReference>, CMakeProjectReference[]>(pr => ProjectDependencyUtils.OrderProjectReferencesByDependencies(pr, allProjects, logger)));
        scriptObject.Add("get_directory_name", DelegateCustomFunction.CreateFunc<string?, string?>(Path.GetDirectoryName));
        scriptObject.Add("get_relative_path", DelegateCustomFunction.CreateFunc<string, string, string>((path, relativeTo) => Path.GetRelativePath(relativeTo, path)));
        scriptObject.Add("prepend_relative_paths_with_cmake_current_source_dir", DelegateCustomFunction.CreateFunc<CMakeExpression, CMakeExpression>(PrependRelativePathsWithCMakeCurrentSourceDir));

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

    static CMakeExpression PrependRelativePathsWithCMakeCurrentSourceDir(CMakeExpression normalizedPath)
    {
        var path = normalizedPath.Value;
        var isAbsolutePath = Path.IsPathRooted(path);

        // if a path starts with a CMake variable, we just assume that the variable resolves to an absolute path
        isAbsolutePath |= path.StartsWith("${");

        if (!isAbsolutePath)
            if (path == ".")
                return CMakeExpression.Expression("${CMAKE_CURRENT_SOURCE_DIR}");
            else
                return CMakeExpression.Expression("${CMAKE_CURRENT_SOURCE_DIR}/" + path);
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
