using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class PreprocessorDefinitionsTests
    {
        static string CreateProjectWithDefines() => $"""
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
                    <ConfigurationType>Application</ConfigurationType>
                    <CharacterSet>NotSet</CharacterSet>
                </PropertyGroup>
                <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Release|Win32'" Label="Configuration">
                    <ConfigurationType>Application</ConfigurationType>
                    <CharacterSet>NotSet</CharacterSet>
                </PropertyGroup>
                <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Debug|Win32'">
                    <ClCompile>
                        <PreprocessorDefinitions>FOO;DEBUG;FOO;VALUE=1;%(PreprocessorDefinitions)</PreprocessorDefinitions>
                    </ClCompile>
                </ItemDefinitionGroup>
                <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Release|Win32'">
                    <ClCompile>
                        <PreprocessorDefinitions>FOO;NDEBUG;VALUE=2;%(PreprocessorDefinitions)</PreprocessorDefinitions>
                    </ClCompile>
                </ItemDefinitionGroup>
            </Project>
            """;

        static string CreateProjectWithMBCS() => $"""
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
                    <ConfigurationType>Application</ConfigurationType>
                    <CharacterSet>MultiByte</CharacterSet>
                </PropertyGroup>
                <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Release|Win32'" Label="Configuration">
                    <ConfigurationType>Application</ConfigurationType>
                    <CharacterSet>MultiByte</CharacterSet>
                </PropertyGroup>
                <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Debug|Win32'">
                    <ClCompile>
                        <PreprocessorDefinitions>DEBUG_DEF;%(PreprocessorDefinitions)</PreprocessorDefinitions>
                    </ClCompile>
                </ItemDefinitionGroup>
                <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Release|Win32'">
                    <ClCompile>
                        <PreprocessorDefinitions>RELEASE_DEF;%(PreprocessorDefinitions)</PreprocessorDefinitions>
                    </ClCompile>
                </ItemDefinitionGroup>
            </Project>
            """;

        static string CreateProjectWithInvalidCharSet() => $"""
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
                    <ConfigurationType>Application</ConfigurationType>
                    <CharacterSet>InvalidCharSet</CharacterSet>
                </PropertyGroup>
                <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Release|Win32'" Label="Configuration">
                    <ConfigurationType>Application</ConfigurationType>
                    <CharacterSet>InvalidCharSet</CharacterSet>
                </PropertyGroup>
            </Project>
            """;

        static string CreateProjectWithArchDefines() => $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Project DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
                <ItemGroup Label="ProjectConfigurations">
                    <ProjectConfiguration Include="Debug|Win32">
                        <Configuration>Debug</Configuration>
                        <Platform>Win32</Platform>
                    </ProjectConfiguration>
                    <ProjectConfiguration Include="Debug|x64">
                        <Configuration>Debug</Configuration>
                        <Platform>x64</Platform>
                    </ProjectConfiguration>
                </ItemGroup>
                <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Debug|Win32'" Label="Configuration">
                    <ConfigurationType>Application</ConfigurationType>
                </PropertyGroup>
                <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Debug|x64'" Label="Configuration">
                    <ConfigurationType>Application</ConfigurationType>
                </PropertyGroup>
                <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Debug|Win32'">
                    <ClCompile>
                        <PreprocessorDefinitions>X86_DEF;%(PreprocessorDefinitions)</PreprocessorDefinitions>
                    </ClCompile>
                </ItemDefinitionGroup>
                <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Debug|x64'">
                    <ClCompile>
                        <PreprocessorDefinitions>X64_DEF;%(PreprocessorDefinitions)</PreprocessorDefinitions>
                    </ClCompile>
                </ItemDefinitionGroup>
            </Project>
            """;

        [Fact]
        public void Given_ProjectWithConfigurationSpecificDefines_When_Converted_Then_GeneratorExpressionsUsedAndDuplicatesRemoved()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProjectWithDefines()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            AssertEx.FileHasContent(@"CMakeLists.txt", fileSystem, """
                cmake_minimum_required(VERSION 3.15)
                project(Project)


                add_executable(Project
                )

                target_compile_definitions(Project
                    PUBLIC
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
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"ProjectMBCS.vcxproj", new(CreateProjectWithMBCS()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"ProjectMBCS.vcxproj")]);

            AssertEx.FileHasContent(@"CMakeLists.txt", fileSystem, """
                cmake_minimum_required(VERSION 3.15)
                project(ProjectMBCS)


                add_executable(ProjectMBCS
                )

                target_compile_definitions(ProjectMBCS
                    PUBLIC
                        _MBCS
                        $<$<CONFIG:Debug>:DEBUG_DEF>
                        $<$<CONFIG:Release>:RELEASE_DEF>
                )
                """);
        }

        [Fact]
        public void Given_ProjectWithInvalidCharacterSet_When_Converted_Then_Throws()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProjectWithInvalidCharSet()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            var ex = Assert.Throws<CatastrophicFailureException>(() =>
                converter.Convert(
                    projectFiles: [new(@"Project.vcxproj")]));

            Assert.Contains("Invalid value for CharacterSet", ex.Message);
        }

        [Fact]
        public void Given_ProjectWithArchitectureSpecificDefines_When_Converted_Then_UsesGeneratorExpressionsForArchitecture()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"ProjectArch.vcxproj", new(CreateProjectWithArchDefines()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"ProjectArch.vcxproj")]);

            AssertEx.FileHasContent(@"CMakeLists.txt", fileSystem, """
                cmake_minimum_required(VERSION 3.15)
                project(ProjectArch)


                add_executable(ProjectArch
                )

                target_compile_definitions(ProjectArch
                    PUBLIC
                        $<$<STREQUAL:$<CMAKE_CXX_COMPILER_ARCHITECTURE_ID>,X86>:X86_DEF>
                        $<$<STREQUAL:$<CMAKE_CXX_COMPILER_ARCHITECTURE_ID>,x64>:X64_DEF>
                )
                """);
        }
    }
}
