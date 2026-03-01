using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class MfcSupportTests
    {
        static string CreateProjectWithUseOfMfc(string debugUseOfMfc, string releaseUseOfMfc) => $"""
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
                <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Debug|Win32'" Label="Configuration">
                    <UseDebugLibraries>true</UseDebugLibraries>
                    <UseOfMfc>{debugUseOfMfc}</UseOfMfc>
                </PropertyGroup>
                <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Release|Win32'" Label="Configuration">
                    <UseDebugLibraries>false</UseDebugLibraries>
                    <UseOfMfc>{releaseUseOfMfc}</UseOfMfc>
                </PropertyGroup>
            </Project>
            """;

        [Fact]
        public void Given_ProjectWithStaticMfc_When_Converted_Then_CMakeMfcFlagSetTo1AndAfxdllDefinitionAdded()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProjectWithUseOfMfc("Static", "Static")));

            // Act
            var converter = new Converter(fileSystem, NullLogger.Instance);
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("""
                set_target_properties(Project PROPERTIES
                    CMAKE_MFC_FLAG 1
                )
                """.TrimEnd(), cmake);
            Assert.Contains("""
                target_compile_definitions(Project
                    PUBLIC
                        _AFXDLL
                )
                """.TrimEnd(), cmake);
        }

        [Fact]
        public void Given_ProjectWithSharedMfc_When_Converted_Then_CMakeMfcFlagSetTo2AndAfxdllDefinitionAdded()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProjectWithUseOfMfc("Dynamic", "Dynamic")));

            // Act
            var converter = new Converter(fileSystem, NullLogger.Instance);
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("""
                set_target_properties(Project PROPERTIES
                    CMAKE_MFC_FLAG 2
                )
                """.TrimEnd(), cmake);
            Assert.Contains("""
                target_compile_definitions(Project
                    PUBLIC
                        _AFXDLL
                )
                """.TrimEnd(), cmake);
        }

        [Fact]
        public void Given_ProjectWithSharedMfcInRelease_When_Converted_Then_GeneratorExpressionsAreUsedForCMakeMfcFlagAndAfxdll()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProjectWithUseOfMfc("false", "Dynamic")));

            // Act
            var converter = new Converter(fileSystem, NullLogger.Instance);
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("""
                set_target_properties(Project PROPERTIES
                    CMAKE_MFC_FLAG "$<$<CONFIG:Debug>:0>$<$<CONFIG:Release>:2>"
                )
                """.TrimEnd(), cmake);
            Assert.Contains("""
                target_compile_definitions(Project
                    PUBLIC
                        $<$<CONFIG:Release>:_AFXDLL>
                )
                """.TrimEnd(), cmake);
        }

        [Fact]
        public void Given_ProjectWithoutMfc_When_Converted_Then_NoMfcFlagOrAfxdllDefinitionAdded()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProjectWithUseOfMfc("false", "false")));

            // Act
            var converter = new Converter(fileSystem, NullLogger.Instance);
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.DoesNotContain("CMAKE_MFC_FLAG", cmake);
            Assert.DoesNotContain("_AFXDLL", cmake);
        }
    }
}
