namespace vcxproj2cmake;

class CMakeSolution
{
    public string AbsoluteSolutionPath { get; }
    public string SolutionName { get; }
    public CMakeProjectReference[] Projects { get; }

    public CMakeSolution(MSBuildSolution solution, IEnumerable<CMakeProject> projects)
    {
        AbsoluteSolutionPath = solution.AbsoluteSolutionPath;
        SolutionName = solution.SolutionName;
        Projects = solution.Projects
            .Zip(projects, (path, project) => new CMakeProjectReference { Path = path, Project = project })
            .ToArray();
    }

    public bool SolutionIsTopLevel
    {
        get
        {
            var solutionDir = Path.GetFullPath(Path.GetDirectoryName(AbsoluteSolutionPath)!);

            // this works for absolute and relative project.Path, Path.Combine handles both cases correctly
            return Projects.All(project =>
                Path.GetFullPath(Path.Combine(solutionDir, project.Path)).StartsWith(solutionDir, StringComparison.OrdinalIgnoreCase));
        }
    }
}
