using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;
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
        var extension = Path.GetExtension(solutionPath);

        if (extension.Equals(".sln", StringComparison.OrdinalIgnoreCase))
        {
            var regex = new Regex(@"Project\(.*?\) = .*?, ""(.*?)""");

            foreach (var line in fileSystem.File.ReadLines(solutionPath))
            {
                var match = regex.Match(line);
                if (!match.Success)
                    continue;

                AddProject(match.Groups[1].Value);
            }
        }
        else if (extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase))
        {
            using var stream = fileSystem.File.OpenRead(solutionPath);
            var solutionModel = SolutionSerializers.SlnXml.OpenAsync(stream, CancellationToken.None).GetAwaiter().GetResult();
            foreach (var project in solutionModel.SolutionProjects)
                AddProject(project.FilePath);
        }
        else
            throw new CatastrophicFailureException($"Unsupported solution file format: {extension}. Only .sln or .slnx are supported.");

        return new MSBuildSolution
        {
            AbsoluteSolutionPath = Path.GetFullPath(solutionPath),
            SolutionName = Path.GetFileNameWithoutExtension(solutionPath),
            Projects = projectPaths.ToArray()
        };

        void AddProject(string projectFilePath)
        {
            var normalizedPath = PathUtils.NormalizePathSeparators(projectFilePath);
            if (normalizedPath.EndsWith(".vcxproj", StringComparison.OrdinalIgnoreCase))
                projectPaths.Add(normalizedPath);
            else
                logger.LogWarning($"Ignoring non-vcxproj project: {normalizedPath}");
        }
    }
}
