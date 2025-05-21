using System.Text.RegularExpressions;

class SolutionInfo
{
    public required string AbsoluteSolutionPath { get; init; }
    public required string SolutionName { get; init; }
    public required ProjectReference[] Projects { get; init; }

    public static SolutionInfo ParseSolutionFile(string solutionPath)
    {
        Console.WriteLine($"Parsing {solutionPath}");

        var projectPaths = new List<string>();
        var regex = new Regex(@"Project\(.*?\) = .*?, ""(.*?\.vcxproj)""", RegexOptions.IgnoreCase);

        foreach (var line in File.ReadLines(solutionPath))
        {
            var match = regex.Match(line);
            if (match.Success)
                projectPaths.Add(match.Groups[1].Value);
        }

        return new SolutionInfo
        {
            AbsoluteSolutionPath = Path.GetFullPath(solutionPath),
            SolutionName = Path.GetFileNameWithoutExtension(solutionPath),
            Projects = projectPaths.Select(p => new ProjectReference { Path = p }).ToArray()
        };
    }
}
