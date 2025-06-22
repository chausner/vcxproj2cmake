using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class ConfigDependentSettingsTests
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
                <PropertyGroup>
                    <ConfigurationType>Application</ConfigurationType>
                </PropertyGroup>
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

        static string CreateProjectWithPlatformLibraryDirs(string win32Dirs, string x64Dirs) => $"""
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
                <PropertyGroup>
                    <ConfigurationType>Application</ConfigurationType>
                </PropertyGroup>
                <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Debug|Win32'">
                    <Link>
                        <AdditionalLibraryDirectories>{win32Dirs}</AdditionalLibraryDirectories>
                    </Link>
                </ItemDefinitionGroup>
                <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Debug|x64'">
                    <Link>
                        <AdditionalLibraryDirectories>{x64Dirs}</AdditionalLibraryDirectories>
                    </Link>
                </ItemDefinitionGroup>
            </Project>
            """;

        static string CreateProjectWithCombinationSpecificLibraryDirs() => """
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
                    <ProjectConfiguration Include="Release|Win32">
                        <Configuration>Release</Configuration>
                        <Platform>Win32</Platform>
                    </ProjectConfiguration>
                    <ProjectConfiguration Include="Release|x64">
                        <Configuration>Release</Configuration>
                        <Platform>x64</Platform>
                    </ProjectConfiguration>
                </ItemGroup>
                <PropertyGroup>
                    <ConfigurationType>Application</ConfigurationType>
                </PropertyGroup>
                <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Debug|Win32'">
                    <Link>
                        <AdditionalLibraryDirectories>DebugWin32</AdditionalLibraryDirectories>
                    </Link>
                </ItemDefinitionGroup>
                <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Debug|x64'">
                    <Link>
                        <AdditionalLibraryDirectories>DebugX64</AdditionalLibraryDirectories>
                    </Link>
                </ItemDefinitionGroup>
                <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Release|Win32'">
                    <Link>
                        <AdditionalLibraryDirectories>ReleaseWin32</AdditionalLibraryDirectories>
                    </Link>
                </ItemDefinitionGroup>
                <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Release|x64'">
                    <Link>
                        <AdditionalLibraryDirectories>ReleaseX64</AdditionalLibraryDirectories>
                    </Link>
                </ItemDefinitionGroup>
            </Project>
            """;

        static string CreateProjectWithUnconditionalLibraryDirs(string dirs) => $"""
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
                <PropertyGroup>
                    <ConfigurationType>Application</ConfigurationType>
                </PropertyGroup>
                <ItemDefinitionGroup>
                    <Link>
                        <AdditionalLibraryDirectories>{dirs}</AdditionalLibraryDirectories>
                    </Link>
                </ItemDefinitionGroup>
            </Project>
            """;

        static string CreateProjectWithOverwrittenLibraryDirs() => """
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
                <PropertyGroup>
                    <ConfigurationType>Application</ConfigurationType>
                </PropertyGroup>
                <ItemDefinitionGroup>
                    <Link>
                        <AdditionalLibraryDirectories>Libs1</AdditionalLibraryDirectories>
                    </Link>
                </ItemDefinitionGroup>
                <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Debug|Win32'">
                    <Link>
                        <AdditionalLibraryDirectories>DebugLib1</AdditionalLibraryDirectories>
                    </Link>
                </ItemDefinitionGroup>
                <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Release|Win32'">
                    <Link>
                        <AdditionalLibraryDirectories>ReleaseLib1</AdditionalLibraryDirectories>
                    </Link>
                </ItemDefinitionGroup>
                <ItemDefinitionGroup>
                    <Link>
                        <AdditionalLibraryDirectories>Libs2</AdditionalLibraryDirectories>
                    </Link>
                </ItemDefinitionGroup>
                <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Debug|Win32'">
                    <Link>
                        <AdditionalLibraryDirectories>DebugLib2</AdditionalLibraryDirectories>
                    </Link>
                </ItemDefinitionGroup>
            </Project>
            """;

        [Fact]
        public void Given_LinkerPathsWithoutCondition_When_Converted_Then_TargetLinkDirectoriesAdded()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProjectWithUnconditionalLibraryDirs("C:/Lib")));

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
        public void Given_LinkerPathsSameForAllConfigs_When_Converted_Then_TargetLinkDirectoriesAdded()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProjectWithLibraryDirs("C:/Lib/", "C:/Lib/")));

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
        public void Given_LinkerPathsDifferentPerPlatform_When_Converted_Then_GeneratorExpressionsUseArchitecture()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProjectWithPlatformLibraryDirs("Win32Lib", "X64Lib")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("""
                target_link_directories(Project
                    PUBLIC
                        $<$<STREQUAL:$<CMAKE_CXX_COMPILER_ARCHITECTURE_ID>,X86>:Win32Lib>
                        $<$<STREQUAL:$<CMAKE_CXX_COMPILER_ARCHITECTURE_ID>,x64>:X64Lib>
                )
                """.Trim(), cmake);
        }

        [Fact]
        public void Given_LinkerPathsDifferentPerConfigAndPlatform_When_Converted_Then_SkippedWithWarning()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProjectWithCombinationSpecificLibraryDirs()));

            var logger = new InMemoryLogger();
            var converter = new Converter(fileSystem, logger);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.DoesNotContain("target_link_directories(Project", cmake);
            Assert.Contains("ignored because they are specific to certain build configurations", logger.AllMessageText);
        }

        [Fact]
        public void Given_LinkerPathsOverwrittenMultipleTimes_When_Converted_Then_LastValueUsed()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProjectWithOverwrittenLibraryDirs()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("""
                target_link_directories(Project
                    PUBLIC
                        $<$<CONFIG:Debug>:DebugLib2>
                        $<$<CONFIG:Release>:Libs2>
                )
                """.Trim(), cmake);
            Assert.DoesNotContain("DebugLib1", cmake);
            Assert.DoesNotContain("Libs1", cmake);
            Assert.DoesNotContain("ReleaseLib1", cmake);
        }
    }
}
