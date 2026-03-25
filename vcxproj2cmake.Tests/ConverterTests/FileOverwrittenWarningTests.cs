using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class FileOverwrittenWarningTests
    {
        [Fact]
        public void Given_CMakeOutputFilesDoNotExist_When_Converted_Then_NoWarningsAreLogged()
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
                solutionFile: new(@"TwoEmptyProjects.sln"));

            // Assert
            Assert.DoesNotMatch(@"File .* already exists and will be overwritten\.", logger.AllMessageText);
        }

        [Fact]
        public void Given_CMakeOutputFilesAlreadyExist_When_Converted_Then_WarningsAreLogged()
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

            fileSystem.AddFile(Path.Combine("EmptyProject1", "CMakeLists.txt"), new(string.Empty));
            fileSystem.AddFile(Path.Combine("EmptyProject2", "CMakeLists.txt"), new(string.Empty));
            fileSystem.AddFile("CMakeLists.txt", new(string.Empty));

            var logger = new InMemoryLogger();
            var converter = new Converter(fileSystem, logger);

            // Act
            converter.Convert(
                solutionFile: new(@"TwoEmptyProjects.sln"));

            // Assert
            Assert.Contains($"File {Path.GetFullPath(Path.Combine("EmptyProject1", "CMakeLists.txt"))} already exists and will be overwritten.", logger.AllMessageText);
            Assert.Contains($"File {Path.GetFullPath(Path.Combine("EmptyProject2", "CMakeLists.txt"))} already exists and will be overwritten.", logger.AllMessageText);
            Assert.Contains($"File {Path.GetFullPath("CMakeLists.txt")} already exists and will be overwritten.", logger.AllMessageText);
        }

        [Fact]
        public void Given_CMakeOutputFilesDoNotExist_When_ConvertedWithDryRun_Then_NoWarningsAreLogged()
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

            fileSystem.AddFile(Path.Combine("EmptyProject1", "CMakeLists.txt"), new(string.Empty));
            fileSystem.AddFile(Path.Combine("EmptyProject2", "CMakeLists.txt"), new(string.Empty));
            fileSystem.AddFile("CMakeLists.txt", new(string.Empty));

            var logger = new InMemoryLogger();
            var converter = new Converter(fileSystem, logger);

            // Act
            converter.Convert(
                solutionFile: new(@"TwoEmptyProjects.sln"),
                dryRun: true);

            // Assert
            Assert.DoesNotMatch(@"File .* already exists and will be overwritten\.", logger.AllMessageText);
        }
    }
}
