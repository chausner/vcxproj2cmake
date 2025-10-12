using Microsoft.Extensions.Logging;
using Scriban;
using Scriban.Runtime;
using System.IO.Abstractions;
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

        var scriptObject = new ScriptObject();
        scriptObject.Import(model);
        scriptObject.Import(settings);
        scriptObject.Import("fail", new Action<string>(error => throw new CatastrophicFailureException(error)));
        scriptObject.Import("literal", ToCMakeLiteral);
        scriptObject.Import("unquoted_literal", (string s) => ToCMakeLiteral(s, unquoted: true));
        scriptObject.Import("normalize_path", PathUtils.NormalizePath);
        scriptObject.Import("order_project_references_by_dependencies", (IEnumerable<CMakeProjectReference> pr) => ProjectDependencyUtils.OrderProjectReferencesByDependencies(pr, allProjects, logger));
        scriptObject.Import("get_directory_name", new Func<string?, string?>(Path.GetDirectoryName));
        scriptObject.Import("get_relative_path", new Func<string, string, string?>((path, relativeTo) => Path.GetRelativePath(relativeTo, path)));
        scriptObject.Import("prepend_relative_paths_with_cmake_current_source_dir", PrependRelativePathsWithCMakeCurrentSourceDir);

        var context = new TemplateContext();
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

    private static string ApplyIndentation(string text, IndentStyle indentStyle, int indentSize)
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

        GenerateCMake(project, allProjects, cmakeListsPath, cmakeListsTemplate, settings);
    }

    void GenerateCMakeForSolution(CMakeSolution solution, IEnumerable<CMakeProject> allProjects, Template cmakeListsTemplate, CMakeGeneratorSettings settings)
    {
        string cmakeListsPath = Path.Combine(Path.GetDirectoryName(solution.AbsoluteSolutionPath)!, "CMakeLists.txt");

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
                // commented out for now since it conflicts with TranslateMSBuildMacros
                //'$' => "\\$",   // prevent ${VAR} expansion
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