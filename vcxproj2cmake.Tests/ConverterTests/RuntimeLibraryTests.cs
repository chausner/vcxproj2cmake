using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class RuntimeLibraryTests
    {
        [Fact]
        public void Given_RuntimeLibrarySetToDefaultsInAllConfigs_When_Converted_Then_NoSetTargetPropertiesMsvcRuntimeLibraryAdded()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithClCompileSetting("RuntimeLibrary", debugValue: "MultiThreadedDebugDLL", releaseValue: "MultiThreadedDLL")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.DoesNotContain("MSVC_RUNTIME_LIBRARY", cmake);
        }

        [Fact]
        public void Given_RuntimeLibrarySetEquallyInAllConfigs_When_Converted_Then_SetTargetPropertiesMsvcRuntimeLibraryAdded()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithClCompileSetting("RuntimeLibrary", "MultiThreaded")
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
                    MSVC_RUNTIME_LIBRARY MultiThreaded
                )
                """,
                cmake);
        }

        [Fact]
        public void Given_RuntimeLibrarySetToCustomConfigDependentValue_When_Converted_Then_SetTargetPropertiesMsvcRuntimeLibraryAddedWithCMakeExpression()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithClCompileSetting("RuntimeLibrary", debugValue: "MultiThreadedDebug", releaseValue: "MultiThreadedDLL")
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
                    MSVC_RUNTIME_LIBRARY $<$<CONFIG:Debug>:MultiThreadedDebug>$<$<CONFIG:Release>:MultiThreadedDLL>
                )
                """,
                cmake);
        }

        [Fact]
        public void Given_RuntimeLibrarySetToNonDllConfigDependentValue_When_Converted_Then_SetTargetPropertiesMsvcRuntimeLibraryAddedWithSimpleCMakeExpression()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithClCompileSetting("RuntimeLibrary", debugValue: "MultiThreadedDebug", releaseValue: "MultiThreaded")
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
                    MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>"
                )
                """,
                cmake);
        }
    }
}
