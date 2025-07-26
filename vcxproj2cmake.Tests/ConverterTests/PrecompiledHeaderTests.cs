using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class PrecompiledHeaderTests
    {
        static string CreateProject(
            string debugMode,
            string releaseMode,
            string debugHeader = "pch.h",
            string releaseHeader = "pch.h") => $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Project DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
                <ItemGroup Label="ProjectConfigurations">
                    <ProjectConfiguration Include="Debug|Win32">
                        <Configuration>Debug</Configuration>
                        <Platform>Win32</Platform>
                    </ProjectConfiguration>
                    <ProjectConfiguration Include="Release|Win32">
                        <Configuration>Release</Configuration>
                        <Platform>Win32</Platform>
                    </ProjectConfiguration>
                </ItemGroup>
                <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Debug|Win32'">
                    <ClCompile>
                        <PrecompiledHeader>{debugMode}</PrecompiledHeader>
                        <PrecompiledHeaderFile>{debugHeader}</PrecompiledHeaderFile>
                    </ClCompile>
                </ItemDefinitionGroup>
                <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Release|Win32'">
                    <ClCompile>
                        <PrecompiledHeader>{releaseMode}</PrecompiledHeader>
                        <PrecompiledHeaderFile>{releaseHeader}</PrecompiledHeaderFile>
                    </ClCompile>
                </ItemDefinitionGroup>
            </Project>
            """;

        [Fact]
        public void Given_PrecompiledHeaderUsedInAllConfigs_When_Converted_Then_TargetPrecompileHeadersAdded()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProject("Use", "Use")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains(
                """
                target_precompile_headers(Project
                    PRIVATE
                        ${CMAKE_CURRENT_SOURCE_DIR}/pch.h
                )
                """,
                cmake);
        }

        [Fact]
        public void Given_PrecompiledHeaderUsedOnlyForDebug_When_Converted_Then_GeneratorExpressionWritten()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProject("Use", "NotUsing")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

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
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProject("Use", "Use", "pch_debug.h", "pch_release.h")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

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
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProject("NotUsing", "NotUsing")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.DoesNotContain("target_precompile_headers(Project", cmake);
        }
    }
}
