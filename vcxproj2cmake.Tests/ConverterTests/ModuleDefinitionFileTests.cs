using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class ModuleDefinitionFileTests
    {
        [Fact]
        public void Given_ProjectWithModuleDefinitionFile_When_Converted_Then_TargetSourcesListsDefFile()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new("""
                <?xml version="1.0" encoding="utf-8"?>
                <Project DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
                    <ItemGroup Label="ProjectConfigurations">
                        <ProjectConfiguration Include="Debug|Win32">
                            <Configuration>Debug</Configuration>
                            <Platform>Win32</Platform>
                        </ProjectConfiguration>
                    </ItemGroup>
                    <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Debug|Win32'" Label="Configuration">
                        <UseDebugLibraries>true</UseDebugLibraries>
                    </PropertyGroup>
                    <ItemGroup>
                        <ClCompile Include="src\main.cpp" />
                    </ItemGroup>
                    <ItemDefinitionGroup>
                        <Link>
                            <ModuleDefinitionFile>project.def</ModuleDefinitionFile>
                        </Link>
                    </ItemDefinitionGroup>
                </Project>
                """));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(projectFiles: [new(@"Project.vcxproj")]);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("""
                target_sources(Project
                    PRIVATE
                        src/main.cpp
                        project.def
                )
                """.Trim(), cmake);
        }

        [Fact]
        public void Given_ProjectWithConfigurationSpecificModuleDefinitionFile_When_Converted_Then_TargetSourcesUsesGeneratorExpressions()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new("""
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
                    <ItemGroup>
                        <ClCompile Include="src\main.cpp" />
                    </ItemGroup>
                    <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Debug|Win32'">
                        <Link>
                            <ModuleDefinitionFile>project_debug.def</ModuleDefinitionFile>
                        </Link>
                    </ItemDefinitionGroup>
                    <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Release|Win32'">
                        <Link>
                            <ModuleDefinitionFile>project_release.def</ModuleDefinitionFile>
                        </Link>
                    </ItemDefinitionGroup>
                </Project>
                """));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(projectFiles: [new(@"Project.vcxproj")]);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("""
                target_sources(Project
                    PRIVATE
                        src/main.cpp
                        "$<$<CONFIG:Debug>:project_debug.def>"
                        "$<$<CONFIG:Release>:project_release.def>"
                )
                """.Trim(), cmake);
        }
    }
}
