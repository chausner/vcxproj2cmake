using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

class SolutionInfo
{
    public required string AbsoluteSolutionPath { get; init; }
    public required string SolutionName { get; init; }
    public required ProjectReference[] Projects { get; init; }

    public static SolutionInfo ParseSolutionFile(string solutionPath, ILogger logger)
    {
        logger.LogInformation($"Parsing {solutionPath}");

        var projectPaths = new List<string>();
        var regex = new Regex(@"Project\(.*?\) = .*?, ""(.*?)""");

        foreach (var line in File.ReadLines(solutionPath))
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

        return new SolutionInfo
        {
            AbsoluteSolutionPath = Path.GetFullPath(solutionPath),
            SolutionName = Path.GetFileNameWithoutExtension(solutionPath),
            Projects = projectPaths.Select(p => new ProjectReference { Path = p }).ToArray()
        };
    }
}
