using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class PreprocessorDefinitionsTests
    {
        [Fact]
        public void Given_ProjectWithConfigurationSpecificDefines_When_Converted_Then_GeneratorExpressionsUsedAndDuplicatesRemoved()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithClCompileSetting(
                    "PreprocessorDefinitions",
                    debugValue: "FOO;DEBUG;FOO;VALUE=1;%(PreprocessorDefinitions)",
                    releaseValue: "FOO;NDEBUG;VALUE=2;%(PreprocessorDefinitions)")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            Assert.FileHasContent(@"CMakeLists.txt", fileSystem, """
                cmake_minimum_required(VERSION 4.0)
                project(Project)

                add_executable(Project)

                target_compile_definitions(Project
                    PRIVATE
                        FOO
                        $<$<CONFIG:Debug>:DEBUG>
                        $<$<CONFIG:Debug>:VALUE=1>
                        $<$<CONFIG:Release>:NDEBUG>
                        $<$<CONFIG:Release>:VALUE=2>
                )
                """);
        }

        [Fact]
        public void Given_ProjectWithMultiByteCharacterSet_When_Converted_Then_MBCSDefinitionAdded()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"ProjectMBCS.vcxproj", new(TestData.Project()
                .WithProperty("Debug", "Win32", "CharacterSet", "MultiByte")
                .WithProperty("Release", "Win32", "CharacterSet", "MultiByte")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"ProjectMBCS.vcxproj")]);

            // Assert
            Assert.FileHasContent(@"CMakeLists.txt", fileSystem, """
                cmake_minimum_required(VERSION 4.0)
                project(ProjectMBCS)

                add_executable(ProjectMBCS)

                target_compile_definitions(ProjectMBCS
                    PRIVATE
                        _MBCS
                )
                """);
        }

        [Fact]
        public void Given_ProjectWithInvalidCharacterSet_When_Converted_Then_Throws()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithProperty("Debug", "Win32", "CharacterSet", "InvalidCharSet")
                .WithProperty("Release", "Win32", "CharacterSet", "InvalidCharSet")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act & Assert
            var ex = Assert.Throws<CatastrophicFailureException>(() =>
                converter.Convert(
                    projectFiles: [new(@"Project.vcxproj")]));

            Assert.Contains("Invalid value for CharacterSet", ex.Message);
        }

        [Fact]
        public void Given_ProjectWithArchitectureSpecificDefines_When_Converted_Then_UsesGeneratorExpressionsForArchitecture()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"ProjectArch.vcxproj", new(TestData.Project()
                .WithConfigurations(("Debug", "Win32"), ("Debug", "x64"))
                .WithItemDefinitionSetting("Debug", "Win32", "ClCompile", "PreprocessorDefinitions", "X86_DEF;%(PreprocessorDefinitions)")
                .WithItemDefinitionSetting("Debug", "x64", "ClCompile", "PreprocessorDefinitions", "X64_DEF;%(PreprocessorDefinitions)")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"ProjectArch.vcxproj")]);

            // Assert
            Assert.FileHasContent(@"CMakeLists.txt", fileSystem, """
                cmake_minimum_required(VERSION 4.0)
                project(ProjectArch)

                add_executable(ProjectArch)

                set_target_properties(ProjectArch PROPERTIES
                    MSVC_RUNTIME_LIBRARY MultiThreadedDebugDLL
                )

                target_compile_definitions(ProjectArch
                    PRIVATE
                        $<$<STREQUAL:${CMAKE_CXX_COMPILER_ARCHITECTURE_ID},X86>:X86_DEF>
                        $<$<STREQUAL:${CMAKE_CXX_COMPILER_ARCHITECTURE_ID},x64>:X64_DEF>
                )
                """);
        }
    }
}
