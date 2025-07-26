using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class LinkerLibraryDirectoriesTests
    {
        static string CreateProjectWithLibraryDirs(string debugDirs, string releaseDirs) => $"""
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
                    <Link>
                        <AdditionalLibraryDirectories>{debugDirs}</AdditionalLibraryDirectories>
                    </Link>
                </ItemDefinitionGroup>
                <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Release|Win32'">
                    <Link>
                        <AdditionalLibraryDirectories>{releaseDirs}</AdditionalLibraryDirectories>
                    </Link>
                </ItemDefinitionGroup>
            </Project>
            """;

        [Fact]
        public void Given_LinkerPathsSameForAllConfigs_When_Converted_Then_TargetLinkDirectoriesAdded()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProjectWithLibraryDirs("C:\\Lib\\", "C:\\Lib\\")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("""
                target_link_directories(Project
                    PUBLIC
                        C:/Lib
                )
                """.Trim(), cmake);
        }

        [Fact]
        public void Given_LinkerPathsDifferentPerConfig_When_Converted_Then_GeneratorExpressionsUsed()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProjectWithLibraryDirs("DebugLibs", "ReleaseLibs")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("""
                target_link_directories(Project
                    PUBLIC
                        $<$<CONFIG:Debug>:DebugLibs>
                        $<$<CONFIG:Release>:ReleaseLibs>
                )
                """.Trim(), cmake);
        }

        [Fact]
        public void Given_LinkerPathsWithMSBuildMacros_When_Converted_Then_MacrosAreTranslated()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProjectWithLibraryDirs("$(ProjectDir)libs;$(Configuration)", "$(ProjectDir)libs;$(Configuration)")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("""
                target_link_directories(Project
                    PUBLIC
                        ${CMAKE_CURRENT_SOURCE_DIR}/libs
                        ${CMAKE_BUILD_TYPE}
                )
                """.Trim(), cmake);
        }
    }
}
