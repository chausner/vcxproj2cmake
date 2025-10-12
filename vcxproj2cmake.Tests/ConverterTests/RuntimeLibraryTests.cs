using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class RuntimeLibraryTests
    {
        static string CreateProject(string debugValue, string releaseValue) => $"""
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
                </PropertyGroup>
                <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Release|Win32'" Label="Configuration">
                    <UseDebugLibraries>false</UseDebugLibraries>
                </PropertyGroup>
                <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Debug|Win32'">
                    <ClCompile>
                        <RuntimeLibrary>{debugValue}</RuntimeLibrary>
                    </ClCompile>
                </ItemDefinitionGroup>
                <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Release|Win32'">
                    <ClCompile>
                        <RuntimeLibrary>{releaseValue}</RuntimeLibrary>
                    </ClCompile>
                </ItemDefinitionGroup>
            </Project>
            """;

        [Fact]
        public void Given_RuntimeLibrarySetToDefaultsInAllConfigs_When_Converted_Then_NoSetTargetPropertiesMsvcRuntimeLibraryAdded()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProject("MultiThreadedDebugDLL", "MultiThreadedDLL")));

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

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProject("MultiThreaded", "MultiThreaded")));

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

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProject("MultiThreadedDebug", "MultiThreadedDLL")));

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

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProject("MultiThreadedDebug", "MultiThreaded")));

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
