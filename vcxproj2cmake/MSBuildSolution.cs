using Microsoft.Extensions.Logging;
using System.IO.Abstractions;
using System.Text.RegularExpressions;

namespace vcxproj2cmake;

class MSBuildSolution
{
    public required string AbsoluteSolutionPath { get; init; }
    public required string SolutionName { get; init; }
    public required string[] Projects { get; init; }

    public static MSBuildSolution ParseSolutionFile(string solutionPath, IFileSystem fileSystem, ILogger logger)
    {
        logger.LogInformation($"Parsing {solutionPath}");

        var projectPaths = new List<string>();
        var regex = new Regex(@"Project\(.*?\) = .*?, ""(.*?)""");

        foreach (var line in fileSystem.File.ReadLines(solutionPath))
        {
            var match = regex.Match(line);
            if (!match.Success)
                continue;

            var projectFilePath = match.Groups[1].Value;
            if (projectFilePath.EndsWith(".vcxproj", StringComparison.OrdinalIgnoreCase))
                projectPaths.Add(projectFilePath);
            else
                logger.LogWarning($"Ignoring non-vcxproj project: {projectFilePath}");
        }

        return new MSBuildSolution
        {
            AbsoluteSolutionPath = Path.GetFullPath(solutionPath),
            SolutionName = Path.GetFileNameWithoutExtension(solutionPath),
            Projects = projectPaths.ToArray()
        };
    }
}
