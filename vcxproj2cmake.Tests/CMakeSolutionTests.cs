using Xunit;

namespace vcxproj2cmake.Tests;

public class CMakeSolutionTests
{
    public class SolutionIsTopLevelTests
    {
        [Fact]
        public void GivenSolutionWithAllProjectsInSolutionDir_ThenReturnsTrue()
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
        public void GivenSolutionWithProjectOutsideSolutionDirAndRelativeReference_ThenReturnsFalse()
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
        public void GivenSolutionWithProjectOutsideSolutionDirAndAbsoluteReference_ThenReturnsFalse()
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
