using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class ContinueOnErrorTests
    {
        [Fact]
        public void Given_ContinueOnErrorEnabled_When_ProjectInSolutionFailsToLoad_Then_LogsErrorAndContinuesWithConvertingOtherProjects()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(Path.Combine("App", "App.vcxproj"), new(TestData.CreateProject("Application", "..\\Missing\\Missing.vcxproj")));
            fileSystem.AddFile("Projects.sln", new("""
                Microsoft Visual Studio Solution File, Format Version 12.00
                # Visual Studio Version 17
                Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "App", "App\App.vcxproj", "{11111111-1111-1111-1111-111111111111}"
                EndProject
                Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Missing", "Missing\Missing.vcxproj", "{22222222-2222-2222-2222-222222222222}"
                EndProject
                """));

            var logger = new InMemoryLogger();
            var converter = new Converter(fileSystem, logger);

            // Act
            converter.Convert(
                solutionFile: new("Projects.sln"),
                continueOnError: true);

            // Assert
            Assert.Matches("Error processing project file Missing(\\\\|/)Missing.vcxproj: ", logger.AllMessageText);

            Assert.True(fileSystem.FileExists(Path.Combine("App", "CMakeLists.txt")), "CMakeLists.txt should be generated for App project");
            Assert.False(fileSystem.FileExists(Path.Combine("Lib", "CMakeLists.txt")), "CMakeLists.txt should not be generated for Lib project");
        }

        [Fact]
        public void Given_ContinueOnErrorEnabled_When_ProjectInSolutionFailsToConvert_Then_LogsErrorAndContinuesWithConvertingOtherProjects()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(Path.Combine("Lib", "Lib.vcxproj"), new(TestData.CreateProject("UnsupportedConfigurationType")));
            fileSystem.AddFile(Path.Combine("App", "App.vcxproj"), new(TestData.CreateProject("Application", "..\\Lib\\Lib.vcxproj")));
            fileSystem.AddFile("Projects.sln", new("""
                Microsoft Visual Studio Solution File, Format Version 12.00
                # Visual Studio Version 17
                Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "App", "App\App.vcxproj", "{11111111-1111-1111-1111-111111111111}"
                EndProject
                Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Lib", "Lib\Lib.vcxproj", "{22222222-2222-2222-2222-222222222222}"
                EndProject
                """));

            var logger = new InMemoryLogger();
            var converter = new Converter(fileSystem, logger);

            // Act
            converter.Convert(
                solutionFile: new("Projects.sln"),
                continueOnError: true);

            // Assert
            Assert.Matches("Error processing project file .*(\\\\|/)Lib(\\\\|/)Lib.vcxproj", logger.AllMessageText);

            Assert.True(fileSystem.FileExists(Path.Combine("App", "CMakeLists.txt")), "CMakeLists.txt should be generated for App project");
            Assert.False(fileSystem.FileExists(Path.Combine("Lib", "CMakeLists.txt")), "CMakeLists.txt should not be generated for Lib project");
        }
    }
}
