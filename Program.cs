using System.CommandLine;
using System.Reflection;

static class Program
{
    static int Main(string[] args)
    {
        var projectOption = new Option<List<string>?>(
            name: "--project",
            description: "Path(s) to .vcxproj file(s)")
        {
            AllowMultipleArgumentsPerToken = true
        };

        var solutionOption = new Option<string?>(
            name: "--solution",
            description: "Path to .sln file");

        var dryRunOption = new Option<bool>(
            name: "--dry-run",
            description: "Print generated output to the console, do not store generated files");

        var rootCommand = new RootCommand("Converts Microsoft Visual C++ projects and solutions to CMake");
        rootCommand.AddOption(projectOption);
        rootCommand.AddOption(solutionOption);
        rootCommand.AddOption(dryRunOption);
        rootCommand.SetHandler(Run, projectOption, solutionOption, dryRunOption);

        return rootCommand.Invoke(args);
    }

    static void Run(List<string>? projects, string? solution, bool dryRun)
    {
        bool hasProjects = projects != null && projects.Count > 0;
        bool hasSolution = !string.IsNullOrEmpty(solution);
        if (hasProjects == hasSolution)
        {
            Console.Error.WriteLine("Error: Specify either --project or --solution, but not both.");
            Environment.Exit(1);
        }

        var conanPackageInfo = LoadConanPackageInfo();

        SolutionInfo? solutionInfo = null;
        List<ProjectInfo> projectInfos = new();

        if (hasProjects)
        {
            foreach (var projectPath in projects!)
            {
                projectInfos.Add(ProjectInfo.ParseProjectFile(projectPath, conanPackageInfo));
            }
        }
        else if (hasSolution)
        {
            solutionInfo = SolutionInfo.ParseSolutionFile(solution!);

            if (solutionInfo.Projects.Length == 0)
            {
                Console.Error.WriteLine($"Error: No .vcxproj files found in solution: {solution}");
                Environment.Exit(1);
            }

            foreach (var projectReference in solutionInfo.Projects)
            {
                string absolutePath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(solution)!, projectReference.Path));
                projectReference.ProjectFileInfo = ProjectInfo.ParseProjectFile(absolutePath, conanPackageInfo);
                projectInfos.Add(projectReference.ProjectFileInfo);
            }
        }

        ResolveProjectReferences(projectInfos);

        CMakeGenerator.Generate(solutionInfo, projectInfos, dryRun);
    }

    static Dictionary<string, ConanPackage> LoadConanPackageInfo()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("vcxproj2cmake.Resources.conan-packages.csv")!;
        using var streamReader = new StreamReader(stream);

        return
            streamReader.ReadToEnd()
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split(','))
            .Select(tokens => (tokens[0], new ConanPackage(tokens[1], tokens[2])))
            .ToDictionary();
    }

    static void ResolveProjectReferences(List<ProjectInfo> projectInfos)
    {
        foreach (var projectInfo in projectInfos)
        {
            foreach (var projectReference in projectInfo.ProjectReferences)
            {
                var absoluteReference = Path.GetFullPath(projectReference.Path, Path.GetDirectoryName(projectInfo.AbsoluteProjectPath)!);

                var referencedProjectFileInfo = projectInfos.FirstOrDefault(p => p.AbsoluteProjectPath == absoluteReference);

                if (referencedProjectFileInfo == null)
                {
                    Console.Error.WriteLine($"Error: Project {projectInfo.AbsoluteProjectPath} references project {absoluteReference} which is not part of the solution or the list of projects.");
                    Environment.Exit(1);
                }

                projectReference.ProjectFileInfo = referencedProjectFileInfo;
            }
        }
    }
}

