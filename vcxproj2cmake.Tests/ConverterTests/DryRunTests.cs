using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class DryRunTests
    {
        [Fact]
        public void Given_SolutionWithTwoEmptyProjects_When_ConvertedWithDryRun_Then_LogsExpectedOutputAndWritesNoFiles()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(Path.Combine("EmptyProject1", "EmptyProject1.vcxproj"), new(TestData.EmptyProject));
            fileSystem.AddFile(Path.Combine("EmptyProject2", "EmptyProject2.vcxproj"), new(TestData.EmptyProject));

            fileSystem.AddFile(@"TwoEmptyProjects.sln", new("""                
                Microsoft Visual Studio Solution File, Format Version 12.00
                # Visual Studio Version 17
                # MinimumVisualStudioVersion = 10.0.40219.1
                Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "EmptyProject1", "EmptyProject1\EmptyProject1.vcxproj", "{11111111-1111-1111-1111-111111111111}"
                EndProject
                Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "EmptyProject2", "EmptyProject2\EmptyProject2.vcxproj", "{22222222-2222-2222-2222-222222222222}"
                EndProject
                """));

            var logger = new InMemoryLogger();
            var converter = new Converter(fileSystem, logger);

            // Act
            converter.Convert(
                solutionFile: new(@"TwoEmptyProjects.sln"),
                dryRun: true);

            // Assert
            Assert.Contains($"Generated output for {Path.GetFullPath(Path.Combine("EmptyProject1", "CMakeLists.txt"))}", logger.AllMessageText);
            Assert.Contains("    add_executable(EmptyProject1", logger.AllMessageText);
            Assert.Contains($"Generated output for {Path.GetFullPath(Path.Combine("EmptyProject2", "CMakeLists.txt"))}", logger.AllMessageText);
            Assert.Contains("    add_executable(EmptyProject2", logger.AllMessageText);
            Assert.Contains($"Generated output for {Path.GetFullPath("CMakeLists.txt")}", logger.AllMessageText);
            Assert.Contains("    add_subdirectory(EmptyProject1)", logger.AllMessageText);
            Assert.Contains("    add_subdirectory(EmptyProject2)", logger.AllMessageText);

            // Ensure no files were written to disk
            Assert.False(fileSystem.FileExists("CMakeLists.txt"));
            Assert.False(fileSystem.FileExists(Path.Combine("EmptyProject1", "CMakeLists.txt")));
            Assert.False(fileSystem.FileExists(Path.Combine("EmptyProject2", "CMakeLists.txt")));
        }
    }
}
