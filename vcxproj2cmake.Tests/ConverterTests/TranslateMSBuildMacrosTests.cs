using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class TranslateMSBuildMacrosTests
    {
        static string CreateProject(params string[] sources)
            => $"""
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
            {(sources.Length > 0 ? $"""
            <ItemGroup>
                {string.Join(Environment.NewLine, sources.Select(s => $"                <ClCompile Include=\"{s}\" />"))}
            </ItemGroup>
            """ : string.Empty)}
        </Project>
        """;

        [Fact]
        public void Given_ProjectPropertiesWithMSBuildMacros_When_Converted_Then_MacrosAreReplacedByCMakeEquivalents()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProject(
                "$(Configuration).cpp",
                "$(ConfigurationName).cpp",
                "$(ProjectDir)SomeFile.cpp",
                "$(ProjectName).cpp",
                "$(SolutionDir)SomeFile.cpp",
                "$(SolutionName).cpp")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("""
                add_executable(Project
                    "${CMAKE_BUILD_TYPE}.cpp"
                    "${CMAKE_BUILD_TYPE}.cpp"
                    "${CMAKE_CURRENT_SOURCE_DIR}/SomeFile.cpp"
                    "${CMAKE_PROJECT_NAME}.cpp"
                    "${CMAKE_SOURCE_DIR}/SomeFile.cpp"
                    "${PROJECT_NAME}.cpp"
                )
                """, cmake);
        }
    }
}
