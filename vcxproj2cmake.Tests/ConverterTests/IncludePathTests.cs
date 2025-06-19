using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class IncludePathTests
    {
        static string CreateProject(
            string debugIncludes,
            string releaseIncludes,
            string? publicIncludes = null,
            string? allPublic = null) => $"""
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
                <ConfigurationType>StaticLibrary</ConfigurationType>
                {(publicIncludes != null ? $"<PublicIncludeDirectories>{publicIncludes}</PublicIncludeDirectories>" : string.Empty)}
                {(allPublic != null ? $"<AllProjectIncludesArePublic>{allPublic}</AllProjectIncludesArePublic>" : string.Empty)}
            </PropertyGroup>
            <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Debug|Win32'">
                <ClCompile>
                    <AdditionalIncludeDirectories>{debugIncludes}</AdditionalIncludeDirectories>
                </ClCompile>
            </ItemDefinitionGroup>
            <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Release|Win32'">
                <ClCompile>
                    <AdditionalIncludeDirectories>{releaseIncludes}</AdditionalIncludeDirectories>
                </ClCompile>
            </ItemDefinitionGroup>
        </Project>
        """;

        [Fact]
        public void Given_ProjectWithIncludeDirectories_When_Converted_Then_PathsAreWritten()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProject("$(ProjectDir)include;..\\shared", "$(ProjectDir)include;..\\shared")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("""
                target_include_directories(Project
                    PUBLIC
                        ${CMAKE_CURRENT_SOURCE_DIR}/include
                        ${CMAKE_CURRENT_SOURCE_DIR}/../shared
                )
                """, cmake);
        }

        [Fact]
        public void Given_ProjectWithConfigSpecificIncludeDirectories_When_Converted_Then_GeneratorExpressionsUsed()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProject("debug", "release")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("""
                target_include_directories(Project
                    PUBLIC
                        $<$<CONFIG:Debug>:${CMAKE_CURRENT_SOURCE_DIR}/debug>
                        $<$<CONFIG:Release>:${CMAKE_CURRENT_SOURCE_DIR}/release>
                )
                """, cmake);
        }

        [Fact]
        public void Given_ProjectWithPublicIncludeDirectories_When_Converted_Then_InterfacePathsAreWritten()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProject("", "", "public;..\\common")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("""
                target_include_directories(Project
                    INTERFACE
                        ${CMAKE_CURRENT_SOURCE_DIR}/public
                        ${CMAKE_CURRENT_SOURCE_DIR}/../common
                )
                """, cmake);
            Assert.DoesNotContain("PUBLIC\n", cmake); // only INTERFACE section expected
        }

        [Fact]
        public void Given_AllProjectIncludesArePublic_When_Converted_Then_ProjectDirIsAdded()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProject("", "", null, "true")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("""
                target_include_directories(Project
                    INTERFACE
                        ${CMAKE_CURRENT_SOURCE_DIR}
                )
                """, cmake);
        }

        [Fact]
        public void Given_InvalidAllProjectIncludesArePublicValue_When_Converted_Then_Throws()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProject("", "", null, "foo")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            var ex = Assert.Throws<CatastrophicFailureException>(() => converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]));

            Assert.Contains("Invalid value for AllProjectIncludesArePublic", ex.Message);
        }
    }
}
