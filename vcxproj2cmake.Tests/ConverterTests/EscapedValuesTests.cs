using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class EscapedValuesTests
    {
        static string CreateProject(
            string? propertyGroupContent = null,
            string? itemDefinitionGroupContent = null,
            string? itemGroupContent = null,
            string? importContent = null) => $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Project DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
                <ItemGroup Label="ProjectConfigurations">
                    <ProjectConfiguration Include="Release|Win32">
                        <Configuration>Release</Configuration>
                        <Platform>Win32</Platform>
                    </ProjectConfiguration>
                </ItemGroup>
                {importContent ?? string.Empty}
                <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Release|Win32'" Label="Configuration">
                    <UseDebugLibraries>false</UseDebugLibraries>
                    {propertyGroupContent ?? string.Empty}
                </PropertyGroup>
                <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Release|Win32'">
                    {itemDefinitionGroupContent ?? string.Empty}
                </ItemDefinitionGroup>
                <ItemGroup>
                    {itemGroupContent ?? string.Empty}
                </ItemGroup>
            </Project>
            """;

        [Fact]
        public void Given_EscapedScalarAndListValues_When_Converted_Then_UnescapedValuesAreWrittenWithoutSplittingEscapedSeparators()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);
            fileSystem.AddFile(@"Project.vcxproj", new(CreateProject(
                propertyGroupContent: """
                    <TargetName>My%20Target%23%25</TargetName>
                    """,
                itemDefinitionGroupContent: """
                    <ClCompile>
                        <AdditionalIncludeDirectories>include%3Bdir;folder%20name;%(AdditionalIncludeDirectories)</AdditionalIncludeDirectories>
                        <PreprocessorDefinitions>VALUE=a%3Bb;SECOND=100%25;%(PreprocessorDefinitions)</PreprocessorDefinitions>
                        <AdditionalOptions>/DNAME%3Dfoo%20bar /DVALUE%3D100%25</AdditionalOptions>
                    </ClCompile>
                    """)));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains(
                """
                set_target_properties(Project PROPERTIES
                    OUTPUT_NAME "My Target#%"
                """,
                cmake);
            Assert.Contains(
                """
                target_include_directories(Project
                    PUBLIC
                        "${CMAKE_CURRENT_SOURCE_DIR}/include;dir"
                        "${CMAKE_CURRENT_SOURCE_DIR}/folder name"
                )
                """,
                cmake);
            Assert.Contains(
                """
                target_compile_definitions(Project
                    PUBLIC
                        "VALUE=a;b"
                        SECOND=100%
                )
                """,
                cmake);
            Assert.Contains(
                """
                target_compile_options(Project
                    PUBLIC
                        "/DNAME=foo bar"
                        /DVALUE=100%
                )
                """,
                cmake);
        }

        [Fact]
        public void Given_EscapedPathLikeValues_When_Converted_Then_UnescapedPathsImportsAndProjectReferencesAreWritten()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);
            fileSystem.AddFile(@"Project.vcxproj", new(CreateProject(
                importContent: """
                    <Import Project="packages%2Fconan_my%2Dpkg.props" />
                    """,
                itemGroupContent: """
                    <ClCompile Include="src%2Fmain.cpp" />
                    <ClInclude Include="include%5Cmy%23header.hpp" />
                    <ProjectReference Include="libs%2FMy%20Lib.vcxproj" />
                    """)));
            fileSystem.AddFile(Path.Combine("libs", "My Lib.vcxproj"), new(TestData.CreateProject("StaticLibrary")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj"), new(Path.Combine("libs", "My Lib.vcxproj"))],
                includeHeaders: true);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains(
                """
                target_sources(Project
                    PRIVATE
                        "include/my#header.hpp"
                        src/main.cpp
                )
                """,
                cmake);
            Assert.Contains("find_package(my-pkg REQUIRED CONFIG)", cmake);
            Assert.Contains(
                """
                target_link_libraries(Project
                    PUBLIC
                        my-pkg::my-pkg
                        "My Lib"
                )
                """,
                cmake);
        }
    }
}
