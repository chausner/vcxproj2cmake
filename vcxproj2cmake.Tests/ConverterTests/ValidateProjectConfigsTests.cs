using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class ValidateProjectConfigsTests
    {
        [Fact]
        public void Given_ProjectDoesNotUseAnySpecifiedConfiguration_When_Converted_Then_Throws()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile("Project.vcxproj", new(TestData.DefaultEmptyProject));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act & Assert
            var ex = Assert.Throws<CatastrophicFailureException>(() =>
                converter.Convert(
                    projectFiles: [new("Project.vcxproj")],
                    projectConfigs: ["MinSizeRel|Win32", "MinSizeRel|x64"]));

            Assert.Matches("Project .* does not use any of the specified configurations: MinSizeRel\\|Win32, MinSizeRel\\|x64", ex.Message);
        }

        [Fact]
        public void Given_SpecifiedConfigurationIsNotUsedByAnyProject_When_Converted_Then_Throws()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile("Project.vcxproj", new(TestData.DefaultEmptyProject));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act & Assert
            var ex = Assert.Throws<CatastrophicFailureException>(() =>
                converter.Convert(
                    projectFiles: [new("Project.vcxproj")],
                    projectConfigs: ["Debug|Win32", "MinSizeRel|x64"]));

            Assert.Contains("None of the projects uses the specified configuration: MinSizeRel|x64", ex.Message);
        }
    }
}
