using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class PropertiesTests
    {
        [Fact]
        public void Given_TreatWarningAsErrorEnabledForAllConfigs_When_Converted_Then_CompileWarningAsErrorPropertyIsSet()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithClCompileSetting("TreatWarningAsError", "true")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains("""
                set_target_properties(Project PROPERTIES
                    COMPILE_WARNING_AS_ERROR ON
                )
                """.TrimEnd(), cmake);
        }

        [Fact]
        public void Given_TreatWarningAsErrorEnabledConfigSpecific_When_Converted_Then_CompileWarningAsErrorPropertyIsSetWithGeneratorExpression()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithClCompileSetting("TreatWarningAsError", debugValue: "true", releaseValue: "false")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains("""
                set_target_properties(Project PROPERTIES
                    COMPILE_WARNING_AS_ERROR $<$<CONFIG:Debug>:ON>$<$<CONFIG:Release>:OFF>
                )
                """.TrimEnd(), cmake);
        }
    }
}
