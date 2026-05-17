using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class PrecompiledHeaderTests
    {
        [Fact]
        public void Given_PrecompiledHeaderUsedInAllConfigs_When_Converted_Then_TargetPrecompileHeadersAdded()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithItemDefinitionSetting("ClCompile", "PrecompiledHeader", "Use")
                .WithItemDefinitionSetting("ClCompile", "PrecompiledHeaderFile", "pch.h")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains(
                """
                target_precompile_headers(Project
                    PRIVATE
                        "${CMAKE_CURRENT_SOURCE_DIR}/pch.h"
                )
                """,
                cmake);
        }

        [Fact]
        public void Given_PrecompiledHeaderUsedOnlyForDebug_When_Converted_Then_GeneratorExpressionWritten()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithItemDefinitionSetting("Debug", "Win32", "ClCompile", "PrecompiledHeader", "Use")
                .WithItemDefinitionSetting("Debug", "Win32", "ClCompile", "PrecompiledHeaderFile", "pch.h")
                .WithItemDefinitionSetting("Release", "Win32", "ClCompile", "PrecompiledHeader", "NotUsing")
                .WithItemDefinitionSetting("Release", "Win32", "ClCompile", "PrecompiledHeaderFile", "pch.h")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains(
                """
                target_precompile_headers(Project
                    PRIVATE
                        $<$<CONFIG:Debug>:${CMAKE_CURRENT_SOURCE_DIR}/pch.h>
                )
                """,
                cmake);
        }

        [Fact]
        public void Given_PrecompiledHeaderUsesDifferentFilesPerConfig_When_Converted_Then_BothHeadersWritten()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithItemDefinitionSetting("Debug", "Win32", "ClCompile", "PrecompiledHeader", "Use")
                .WithItemDefinitionSetting("Debug", "Win32", "ClCompile", "PrecompiledHeaderFile", "pch_debug.h")
                .WithItemDefinitionSetting("Release", "Win32", "ClCompile", "PrecompiledHeader", "Use")
                .WithItemDefinitionSetting("Release", "Win32", "ClCompile", "PrecompiledHeaderFile", "pch_release.h")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains(
                """
                target_precompile_headers(Project
                    PRIVATE
                        $<$<CONFIG:Debug>:${CMAKE_CURRENT_SOURCE_DIR}/pch_debug.h>
                        $<$<CONFIG:Release>:${CMAKE_CURRENT_SOURCE_DIR}/pch_release.h>
                )
                """,
                cmake);
        }

        [Fact]
        public void Given_PrecompiledHeaderDisabled_When_Converted_Then_NoPrecompileHeaderBlock()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithItemDefinitionSetting("ClCompile", "PrecompiledHeader", "NotUsing")
                .WithItemDefinitionSetting("ClCompile", "PrecompiledHeaderFile", "pch.h")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.DoesNotContain("target_precompile_headers(Project", cmake);
        }
    }
}
