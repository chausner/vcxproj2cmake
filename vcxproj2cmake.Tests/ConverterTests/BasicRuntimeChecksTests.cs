using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class BasicRuntimeChecksTests
    {
        [Fact]
        public void Given_BasicRuntimeChecksUnset_When_Converted_Then_NoSetTargetPropertiesMsvcRuntimeChecksAdded()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.DoesNotContain("MSVC_RUNTIME_CHECKS", cmake);
        }

        [Fact]
        public void Given_BasicRuntimeChecksSetToCMakeDefaultsInAllConfigs_When_Converted_Then_NoSetTargetPropertiesMsvcRuntimeChecksAdded()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithClCompileSetting("BasicRuntimeChecks", debugValue: "EnableFastChecks", releaseValue: "Default")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.DoesNotContain("MSVC_RUNTIME_CHECKS", cmake);
        }

        [Theory]
        [InlineData("StackFrameRuntimeCheck", "StackFrameErrorCheck")]
        [InlineData("UninitializedLocalUsageCheck", "UninitializedVariable")]
        [InlineData("EnableFastChecks", "\"StackFrameErrorCheck;UninitializedVariable\"")]
        [InlineData("Default", "\"\"")]
        public void Given_BasicRuntimeChecksSetEquallyInAllConfigs_When_Converted_Then_SetTargetPropertiesMsvcRuntimeChecksAdded(
            string msbuildValue,
            string cmakeValue)
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithClCompileSetting("BasicRuntimeChecks", msbuildValue)
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains(
                $$"""
                set_target_properties(Project PROPERTIES
                    MSVC_RUNTIME_CHECKS {{cmakeValue}}
                )
                """,
                cmake);
        }

        [Fact]
        public void Given_BasicRuntimeChecksSetToCustomConfigDependentValue_When_Converted_Then_SetTargetPropertiesMsvcRuntimeChecksAddedWithCMakeExpression()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithClCompileSetting("BasicRuntimeChecks", debugValue: "StackFrameRuntimeCheck", releaseValue: "UninitializedLocalUsageCheck")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains(
                """
                set_target_properties(Project PROPERTIES
                    MSVC_RUNTIME_CHECKS "$<$<CONFIG:Debug>:StackFrameErrorCheck>$<$<CONFIG:Release>:UninitializedVariable>"
                )
                """,
                cmake);
        }
    }
}
