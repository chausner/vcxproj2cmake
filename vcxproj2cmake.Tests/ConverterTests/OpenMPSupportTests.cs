using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class OpenMPSupportTests
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
                <PropertyGroup>
                    <ConfigurationType>Application</ConfigurationType>
                </PropertyGroup>
                <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Debug|Win32'">
                    <ClCompile>
                        <OpenMPSupport>{debugValue}</OpenMPSupport>
                    </ClCompile>
                </ItemDefinitionGroup>
                <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Release|Win32'">
                    <ClCompile>
                        <OpenMPSupport>{releaseValue}</OpenMPSupport>
                    </ClCompile>
                </ItemDefinitionGroup>
            </Project>
            """;

        [Fact]
        public void Given_OpenMPEnabledForAllConfigs_When_Converted_Then_LibraryAndPackageAdded()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProject("true", "true")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("find_package(OpenMP REQUIRED)", cmake);
            Assert.Contains("""
                target_link_libraries(Project
                    PUBLIC
                        OpenMP::OpenMP_CXX
                )
                """, cmake);
        }

        [Fact]
        public void Given_OpenMPEnabledOnlyForDebug_When_Converted_Then_LibraryUsesGeneratorExpression()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProject("true", "false")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("find_package(OpenMP REQUIRED)", cmake);
            Assert.Contains("""
                target_link_libraries(Project
                    PUBLIC
                        $<$<CONFIG:Debug>:OpenMP::OpenMP_CXX>
                )
                """, cmake);
        }

        [Fact]
        public void Given_OpenMPDisabled_When_Converted_Then_NoPackageOrLibraryAdded()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProject("false", "false")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.DoesNotContain("find_package(OpenMP REQUIRED)", cmake);
            Assert.DoesNotContain("OpenMP::OpenMP_CXX", cmake);
            Assert.DoesNotContain("target_link_libraries(Project", cmake);
        }
    }
}
