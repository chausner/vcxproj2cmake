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
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.CreateProjectWithClCompileProperty("RuntimeLibrary", "MultiThreadedDebugDLL", "MultiThreadedDLL")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.DoesNotContain("MSVC_RUNTIME_LIBRARY", cmake);
        }

        [Fact]
        public void Given_RuntimeLibrarySetEquallyInAllConfigs_When_Converted_Then_SetTargetPropertiesMsvcRuntimeLibraryAdded()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.CreateProjectWithClCompileProperty("RuntimeLibrary", "MultiThreaded", "MultiThreaded")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

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
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.CreateProjectWithClCompileProperty("RuntimeLibrary", "MultiThreadedDebug", "MultiThreadedDLL")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains(
                """
                set_target_properties(Project PROPERTIES
                    MSVC_RUNTIME_LIBRARY "$<$<CONFIG:Debug>:MultiThreadedDebug>$<$<CONFIG:Release>:MultiThreadedDLL>"
                )
                """,
                cmake);
        }

        [Fact]
        public void Given_RuntimeLibrarySetToNonDllConfigDependentValue_When_Converted_Then_SetTargetPropertiesMsvcRuntimeLibraryAddedWithSimpleCMakeExpression()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.CreateProjectWithClCompileProperty("RuntimeLibrary", "MultiThreadedDebug", "MultiThreaded")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

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
