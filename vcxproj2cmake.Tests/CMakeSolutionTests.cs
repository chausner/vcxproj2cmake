using Xunit;

namespace vcxproj2cmake.Tests;

public class CMakeSolutionTests
{
    [Fact]
    public void When_ProjectCountsDoNotMatch_Then_CtorThrowsArgumentException()
    {
        // Arrange
        var msBuildSolution = new MSBuildSolution
        {
            AbsoluteSolutionPath = Path.GetFullPath("Test.sln"),
            SolutionName = "Test",
            Projects =
            [
                "Project1/Project1.vcxproj", 
                "Project2/Project2.vcxproj"
            ]
        };
        var projects = new[] { (CMakeProject)null! };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new CMakeSolution(msBuildSolution, projects));
    }

    public class SolutionIsTopLevelTests
    {
        [Fact]
        public void Given_SolutionWithAllProjectsInSolutionDir_Then_ReturnsTrue()
        {
            // Arrange
            var msBuildSolution = new MSBuildSolution()
            {
                AbsoluteSolutionPath = Path.GetFullPath("Test.sln"),
                SolutionName = "Test",
                Projects =
                [
                    "Project1/Project1.vcxproj", // relative path
                    Path.GetFullPath("Project2/Project2.vcxproj") // absolute path
                ]
            };
            var solution = new CMakeSolution(msBuildSolution, [null!, null!]);

            // Act & Assert
            Assert.True(solution.SolutionIsTopLevel);
        }

        [Fact]
        public void Given_SolutionWithProjectOutsideSolutionDirAndRelativeReference_Then_ReturnsFalse()
        {
            // Arrange
            var msBuildSolution = new MSBuildSolution
            {
                AbsoluteSolutionPath = Path.GetFullPath("Test.sln"),
                SolutionName = "Test",
                Projects =
                [
                    "../other/Project1.vcxproj"
                ]
            };
            var solution = new CMakeSolution(msBuildSolution, [null!]);

            // Act & Assert
            Assert.False(solution.SolutionIsTopLevel);
        }

        [Fact]
        public void Given_SolutionWithProjectOutsideSolutionDirAndAbsoluteReference_Then_ReturnsFalse()
        {
            // Arrange
            var msBuildSolution = new MSBuildSolution
            {
                AbsoluteSolutionPath = Path.GetFullPath("Test.sln"),
                SolutionName = "Test",
                Projects =
                [
                    Path.GetFullPath("../other/Project1.vcxproj")
                ]
            };
            var solution = new CMakeSolution(msBuildSolution, [null!]);

            // Act & Assert
            Assert.False(solution.SolutionIsTopLevel);
        }        
    }
}
