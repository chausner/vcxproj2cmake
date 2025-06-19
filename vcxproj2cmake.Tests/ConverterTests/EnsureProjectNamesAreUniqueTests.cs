using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class EnsureProjectNamesAreUniqueTests
    {
        [Fact]
        public void Given_SolutionWithDuplicateProjectNames_When_Converted_Then_ProjectsGetUniqueNames()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            // Three projects with the same name in different folders
            fileSystem.AddFile(Path.Combine("Lib", "Project.vcxproj"), new(TestData.EmptyProject));
            fileSystem.AddFile(Path.Combine("App", "Project.vcxproj"), new(TestData.EmptyProject));
            fileSystem.AddFile(Path.Combine("Test", "Project.vcxproj"), new(TestData.EmptyProject));

            fileSystem.AddFile("DuplicateNames.sln", new("""
                Microsoft Visual Studio Solution File, Format Version 12.00
                Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Project", "Lib\Project.vcxproj", "{11111111-1111-1111-1111-111111111111}"
                EndProject
                Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Project", "App\Project.vcxproj", "{22222222-2222-2222-2222-222222222222}"
                EndProject
                Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Project", "Test\Project.vcxproj", "{33333333-3333-3333-3333-333333333333}"
                EndProject
            """));

            var logger = new InMemoryLogger();
            var converter = new Converter(fileSystem, logger);

            // Act
            converter.Convert(
                solutionFile: new("DuplicateNames.sln"),
                dryRun: true);

            // Assert
            Assert.Contains("project(Project)", logger.AllMessageText);
            Assert.Contains("project(Project2)", logger.AllMessageText);
            Assert.Contains("project(Project3)", logger.AllMessageText);
        }

        [Fact]
        public void Given_SolutionWithDuplicateProjectNamesAndNonTopLevel_When_ConvertedWithStandaloneBuilds_Then_BinaryDirsUseUniqueNames()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            // Three projects with the same name in different folders
            fileSystem.AddFile(Path.Combine("Lib", "Project.vcxproj"), new(TestData.EmptyProject));
            fileSystem.AddFile(Path.Combine("App", "Project.vcxproj"), new(TestData.EmptyProject));
            fileSystem.AddFile(Path.Combine("Test", "Project.vcxproj"), new(TestData.EmptyProject));

            fileSystem.AddFile(Path.Combine("Solution", "DuplicateNames.sln"), new("""
                Microsoft Visual Studio Solution File, Format Version 12.00
                Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Project"", "..\Lib\Project.vcxproj", "{11111111-1111-1111-1111-111111111111}"
                EndProject
                Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Project"", "..\App\Project.vcxproj", "{22222222-2222-2222-2222-222222222222}"
                EndProject
                Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Project", "..\Test\Project.vcxproj", "{33333333-3333-3333-3333-333333333333}"
                EndProject
            """));

            var logger = new InMemoryLogger();
            var converter = new Converter(fileSystem, logger);

            // Act
            converter.Convert(
                solutionFile: new(Path.Combine("Solution", "DuplicateNames.sln")),
                enableStandaloneProjectBuilds: true,
                dryRun: true);

            // Assert
            Assert.Contains("""add_subdirectory(../Lib "${CMAKE_BINARY_DIR}/Project")""", logger.AllMessageText);
            Assert.Contains("""add_subdirectory(../App "${CMAKE_BINARY_DIR}/Project2")""", logger.AllMessageText);
            Assert.Contains("""add_subdirectory(../Test "${CMAKE_BINARY_DIR}/Project3")""", logger.AllMessageText);
        }
    }
}
