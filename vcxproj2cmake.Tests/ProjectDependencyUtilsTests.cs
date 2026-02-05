using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public class ProjectDependencyUtilsTests
{
    [Fact]
    public void GivenReferencedProjectMissingFromSolution_WhenOrderingProjects_ThenLogsErrorAndThrows()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

        var appProjectPath = Path.Combine("App", "App.vcxproj");
        var libProjectPath = Path.Combine("Lib", "Lib.vcxproj");

        fileSystem.AddFile(appProjectPath, new MockFileData(TestData.CreateProject("Application", "..\\Lib\\Lib.vcxproj")));
        fileSystem.AddFile(libProjectPath, new MockFileData(TestData.CreateProject("StaticLibrary")));

        var msbuildApp = MSBuildProject.ParseProjectFile(Path.GetFullPath(appProjectPath), fileSystem, NullLogger.Instance);
        var msbuildLib = MSBuildProject.ParseProjectFile(Path.GetFullPath(libProjectPath), fileSystem, NullLogger.Instance);

        var conanRepository = new ConanPackageInfoRepository();
        var cmakeApp = new CMakeProject(msbuildApp, qtVersion: null, conanRepository, NullLogger.Instance);
        var cmakeLib = new CMakeProject(msbuildLib, qtVersion: null, conanRepository, NullLogger.Instance);

        // simulate that Lib exists on disk but is not part of the solution conversion
        cmakeApp.ProjectReferences[0].Project = cmakeLib;

        var logger = new InMemoryLogger();

        // Act
        var exception = Assert.Throws<CatastrophicFailureException>(
            () => ProjectDependencyUtils.OrderProjectsByDependencies([cmakeApp], logger));

        // Assert
        Assert.Equal("Could not determine project dependency tree", exception.Message);
        Assert.Contains("Could not determine project dependency tree", logger.AllMessageText);
        Assert.Contains("Project App" + Environment.NewLine + $"  missing dependency {Path.Combine("..", "Lib", "Lib.vcxproj")}", logger.AllMessageText);
    }
}
