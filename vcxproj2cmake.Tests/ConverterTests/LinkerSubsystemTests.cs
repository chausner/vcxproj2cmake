using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class LinkerSubsystemTests
    {
        [Fact]
        public void Given_ProjectWithWindowsSubsystem_When_Converted_Then_AddExecutableContainsWin32()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithLinkSetting("SubSystem", "Windows")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains("add_executable(Project WIN32", cmake);
        }

        [Fact]
        public void Given_ProjectWithConsoleSubsystem_When_Converted_Then_AddExecutableDoesNotContainWin32()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithLinkSetting("SubSystem", "Console")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.DoesNotContain("WIN32", cmake);
        }

        [Fact]
        public void Given_ProjectWithInconsistentSubsystem_When_Converted_Then_Throws()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithLinkSetting("SubSystem", debugValue: "Windows", releaseValue: "Console")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act & Assert
            var ex = Assert.Throws<CatastrophicFailureException>(() =>
                converter.Convert(
                    projectFiles: [new(@"Project.vcxproj")]));

            Assert.Contains("SubSystem property is inconsistent between configurations", ex.Message);
        }
    }
}
