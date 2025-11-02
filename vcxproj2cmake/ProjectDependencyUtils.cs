using Microsoft.Extensions.Logging;
using System.Text;

namespace vcxproj2cmake;

static class ProjectDependencyUtils
{
    public static CMakeProject[] OrderProjectsByDependencies(IEnumerable<CMakeProject> projects, ILogger? logger = null)
    {
        List<CMakeProject> orderedProjects = [];
        List<CMakeProject> unorderedProjects = projects.OrderBy(p => p.AbsoluteProjectPath).ToList();

        while (unorderedProjects.Count > 0)
        {
            var projectWithAllDependenciesSatisfied = unorderedProjects
                .FirstOrDefault(project => project.ProjectReferences.All(pr => orderedProjects.Any(p2 => p2.AbsoluteProjectPath == pr.Project!.AbsoluteProjectPath)));

            if (projectWithAllDependenciesSatisfied != null)
            {
                orderedProjects.Add(projectWithAllDependenciesSatisfied);
                unorderedProjects.Remove(projectWithAllDependenciesSatisfied);
            }
            else
            {
                if (logger != null)
                {
                    StringBuilder errorMessage = new();
                    errorMessage.AppendLine("Could not determine project dependency tree");

                    foreach (var project in unorderedProjects)
                    {
                        errorMessage.AppendLine($"Project {project.ProjectName}");
                        foreach (var missingReference in project.ProjectReferences.Where(pr => orderedProjects.All(p => p.AbsoluteProjectPath != pr.Project!.AbsoluteProjectPath)))
                        {
                            errorMessage.AppendLine($"  missing dependency {missingReference.Path}");
                        }
                    }

                    logger.LogError(errorMessage.ToString());
                }

                throw new CatastrophicFailureException("Could not determine project dependency tree");
            }
        }

        return orderedProjects.ToArray();
    }

    public static CMakeProjectReference[] OrderProjectReferencesByDependencies(IEnumerable<CMakeProjectReference> projectReferences, IEnumerable<CMakeProject>? allProjects = null, ILogger? logger = null)
    {
        var orderedProjects = OrderProjectsByDependencies(allProjects ?? projectReferences.Select(pr => pr.Project!), logger);

        return projectReferences
            .OrderBy(pr => Array.FindIndex(orderedProjects, p => p.AbsoluteProjectPath == pr.Project!.AbsoluteProjectPath))
            .ToArray();
    }
}
