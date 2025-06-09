using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public class SolutionInfoTests
{
    public class ParseSolutionFileTests
    {
        [Fact]
        public void GivenEmptySolution_ThenParsesSolutionCorrectly()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            var logger = new InMemoryLogger();
            fileSystem.AddFile(@"Empty.sln", new("""
                Microsoft Visual Studio Solution File, Format Version 12.00
                """));

            // Act
            var solutionInfo = SolutionInfo.ParseSolutionFile("Empty.sln", fileSystem, logger);

            // Assert
            Assert.Equal(Path.GetFullPath("Empty.sln"), solutionInfo.AbsoluteSolutionPath);
            Assert.Equal("Empty", solutionInfo.SolutionName);
            Assert.Empty(solutionInfo.Projects);
        }

        [Fact]
        public void GivenNonEmptySolution_ThenParsesSolutionCorrectly()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            var logger = new InMemoryLogger();
            fileSystem.AddFile(@"Test.sln", new("""
                Microsoft Visual Studio Solution File, Format Version 12.00
                Project("{GUID}") = "Project1", "Project1\Project1.vcxproj", "{GUID1}"
                EndProject
                Project("{GUID}") = "Project2", "Project2\Project2.vcxproj", "{GUID2}"
                EndProject
                """));

            // Act
            var solutionInfo = SolutionInfo.ParseSolutionFile("Test.sln", fileSystem, logger);

            // Assert
            Assert.Equal(Path.GetFullPath("Test.sln"), solutionInfo.AbsoluteSolutionPath);
            Assert.Equal("Test", solutionInfo.SolutionName);
            Assert.Equal(2, solutionInfo.Projects.Length);
            Assert.Equal("Project1\\Project1.vcxproj", solutionInfo.Projects[0].Path);
            Assert.Equal("Project2\\Project2.vcxproj", solutionInfo.Projects[1].Path);
        }

        [Fact]
        public void GivenSolutionWithNonVcxprojProject_ThenIgnoresItAndLogsWarning()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            var logger = new InMemoryLogger();
            fileSystem.AddFile(@"Test.sln", new("""
                Microsoft Visual Studio Solution File, Format Version 12.00
                Project("{GUID}") = "Project1", "Project1\Project1.vcxproj", "{GUID1}"
                EndProject
                Project("{GUID}") = "CSharpProject", "CSharpProject\CSharpProject.csproj", "{GUID2}"
                EndProject
                """));

            // Act
            var solutionInfo = SolutionInfo.ParseSolutionFile("Test.sln", fileSystem, logger);

            // Assert
            Assert.Single(solutionInfo.Projects);
            Assert.Equal("Project1\\Project1.vcxproj", solutionInfo.Projects[0].Path);
            Assert.Contains("Ignoring non-vcxproj project: CSharpProject\\CSharpProject.csproj", logger.Messages);
        }
    }

    public class SolutionIsTopLevelTests
    {
        [Fact]
        public void GivenSolutionWithAllProjectsInSolutionDir_ThenReturnsTrue()
        {
            // Arrange
            var solutionInfo = new SolutionInfo
            {
                AbsoluteSolutionPath = Path.GetFullPath("Test.sln"),
                SolutionName = "Test",
                Projects =
                [
                    new ProjectReference { Path = "Project1/Project1.vcxproj" }, // relative path
                    new ProjectReference { Path = Path.GetFullPath("Project2/Project2.vcxproj") } // absolute path
                ]
            };

            // Act & Assert
            Assert.True(solutionInfo.SolutionIsTopLevel);
        }

        [Fact]
        public void GivenSolutionWithProjectOutsideSolutionDirAndRelativeReference_ThenReturnsFalse()
        {
            // Arrange
            var solutionInfo = new SolutionInfo
            {
                AbsoluteSolutionPath = Path.GetFullPath("Test.sln"),
                SolutionName = "Test",
                Projects =
                [
                    new ProjectReference { Path = "../other/Project1.vcxproj" }
                ]
            };

            // Act & Assert
            Assert.False(solutionInfo.SolutionIsTopLevel);
        }

        [Fact]
        public void GivenSolutionWithProjectOutsideSolutionDirAndAbsoluteReference_ThenReturnsFalse()
        {
            // Arrange
            var solutionInfo = new SolutionInfo
            {
                AbsoluteSolutionPath = Path.GetFullPath("Test.sln"),
                SolutionName = "Test",
                Projects =
                [
                    new ProjectReference { Path = Path.GetFullPath("../other/Project1.vcxproj") }
                ]
            };

            // Act & Assert
            Assert.False(solutionInfo.SolutionIsTopLevel);
        }        
    }
}
