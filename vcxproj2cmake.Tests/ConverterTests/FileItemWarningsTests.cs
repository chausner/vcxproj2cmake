using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class FileItemWarningsTests
    {
        static string CreateProjectWithFileItems(string itemGroupXml) => $"""
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
                {itemGroupXml}
            </Project>
            """;

        [Fact]
        public void Given_ProjectWithFileLevelMSBuildSettings_When_Converted_Then_LogsWarning()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);
            var expectedFilePath = PathUtils.NormalizePathSeparators(@"src\main.cpp");
            fileSystem.AddFile(
                @"Project.vcxproj",
                new MockFileData(CreateProjectWithFileItems(
                    """
                    <ItemGroup>
                        <ClCompile Include="src\main.cpp">
                            <ExcludeFromBuild>true</ExcludeFromBuild>
                            <PrecompiledHeader>NotUsing</PrecompiledHeader>
                        </ClCompile>
                    </ItemGroup>
                    """)));

            var logger = new InMemoryLogger();
            var converter = new Converter(fileSystem, logger);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")],
                dryRun: true);

            Assert.Contains(
                $"File-level MSBuild settings are unsupported and will not be processed: {expectedFilePath} (ExcludeFromBuild, PrecompiledHeader)",
                logger.AllMessageText);
        }

        [Fact]
        public void Given_ProjectWithoutFileLevelMSBuildSettings_When_Converted_Then_LogsNoWarning()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);
            fileSystem.AddFile(
                @"Project.vcxproj",
                new MockFileData(CreateProjectWithFileItems(
                    """
                    <ItemGroup>
                        <ClCompile Include="src\main.cpp" />
                        <ClInclude Include="include\main.h" />
                    </ItemGroup>
                    """)));

            var logger = new InMemoryLogger();
            var converter = new Converter(fileSystem, logger);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")],
                dryRun: true);

            Assert.DoesNotContain(
                "File-level MSBuild settings are unsupported and will not be processed",
                logger.AllMessageText);
        }
    }
}
