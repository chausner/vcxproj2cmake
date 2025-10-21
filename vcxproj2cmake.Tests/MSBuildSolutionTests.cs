using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public class MSBuildSolutionTests
{
    public class ParseSolutionFileTests
    {
        [Fact]
        public void GivenEmptySlnSolution_ThenParsesSolutionCorrectly()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            var logger = new InMemoryLogger();
            fileSystem.AddFile(@"Empty.sln", new("""
                Microsoft Visual Studio Solution File, Format Version 12.00
                """));

            // Act
            var solution = MSBuildSolution.ParseSolutionFile("Empty.sln", fileSystem, logger);

            // Assert
            Assert.Equal(Path.GetFullPath("Empty.sln"), solution.AbsoluteSolutionPath);
            Assert.Equal("Empty", solution.SolutionName);
            Assert.Empty(solution.Projects);
        }

        [Fact]
        public void GivenSlnSolutionWithProjects_ThenParsesSolutionCorrectly()
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
            var solution = MSBuildSolution.ParseSolutionFile("Test.sln", fileSystem, logger);

            // Assert
            Assert.Equal(Path.GetFullPath("Test.sln"), solution.AbsoluteSolutionPath);
            Assert.Equal("Test", solution.SolutionName);
            Assert.Equal(2, solution.Projects.Length);
            Assert.Equal(Path.Combine("Project1", "Project1.vcxproj"), solution.Projects[0]);
            Assert.Equal(Path.Combine("Project2", "Project2.vcxproj"), solution.Projects[1]);
        }

        [Fact]
        public void GivenSlnSolutionWithNonVcxprojProject_ThenIgnoresItAndLogsWarning()
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
            var solution = MSBuildSolution.ParseSolutionFile("Test.sln", fileSystem, logger);

            // Assert
            Assert.Single(solution.Projects);
            Assert.Equal(Path.Combine("Project1", "Project1.vcxproj"), solution.Projects[0]);
            Assert.Contains($"Ignoring non-vcxproj project: {Path.Combine("CSharpProject", "CSharpProject.csproj")}", logger.Messages);
        }

        [Fact]
        public void GivenEmptySlnxSolution_ThenParsesSolutionCorrectly()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            var logger = new InMemoryLogger();
            fileSystem.AddFile(@"Empty.slnx", new("""
                <Solution />
                """));

            // Act
            var solution = MSBuildSolution.ParseSolutionFile("Empty.slnx", fileSystem, logger);

            // Assert
            Assert.Equal(Path.GetFullPath("Empty.slnx"), solution.AbsoluteSolutionPath);
            Assert.Equal("Empty", solution.SolutionName);
            Assert.Empty(solution.Projects);
        }

        [Fact]
        public void GivenSlnxSolutionWithProjects_ThenParsesSolutionCorrectly()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            var logger = new InMemoryLogger();
            fileSystem.AddFile(@"Test.slnx", new("""
                <Solution>
                  <Folder Name="/Projects/">
                    <Project Path="Project1\Project1.vcxproj" />
                    <Project Path="Project2\Project2.vcxproj" />
                  </Folder>
                </Solution>
                """));

            // Act
            var solution = MSBuildSolution.ParseSolutionFile("Test.slnx", fileSystem, logger);

            // Assert
            Assert.Equal(Path.GetFullPath("Test.slnx"), solution.AbsoluteSolutionPath);
            Assert.Equal("Test", solution.SolutionName);
            Assert.Equal(2, solution.Projects.Length);
            Assert.Equal(Path.Combine("Project1", "Project1.vcxproj"), solution.Projects[0]);
            Assert.Equal(Path.Combine("Project2", "Project2.vcxproj"), solution.Projects[1]);
        }

        [Fact]
        public void GivenSlnxSolutionWithNonVcxprojProject_ThenIgnoresItAndLogsWarning()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            var logger = new InMemoryLogger();
            fileSystem.AddFile(@"Test.slnx", new("""
                <Solution>
                  <Folder Name="/Projects/">
                    <Project Path="Project1\Project1.vcxproj" />
                    <Project Path="CSharpProject\CSharpProject.csproj" />
                  </Folder>
                </Solution>
                """));

            // Act
            var solution = MSBuildSolution.ParseSolutionFile("Test.slnx", fileSystem, logger);

            // Assert
            Assert.Single(solution.Projects);
            Assert.Equal(Path.Combine("Project1", "Project1.vcxproj"), solution.Projects[0]);
            Assert.Contains($"Ignoring non-vcxproj project: {Path.Combine("CSharpProject", "CSharpProject.csproj")}", logger.Messages);
        }
    }
}
