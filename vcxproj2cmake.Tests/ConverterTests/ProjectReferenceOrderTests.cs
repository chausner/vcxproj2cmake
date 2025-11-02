using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class ProjectReferenceOrderTests
    {
        static string CreateProject(string configurationType = "Application", params string[] projectReferences)
        {
            var references = projectReferences.Length > 0
                ? "<ItemGroup>\n" + string.Join("\n",
                    projectReferences.Select(r => $"    <ProjectReference Include=\"{r}\" />")) +
                  "\n</ItemGroup>"
                : string.Empty;

            return $"""
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
                    <PropertyGroup>
                        <ConfigurationType>{configurationType}</ConfigurationType>
                    </PropertyGroup>
                    {references}
                </Project>
                """;
        }

        [Fact]
        public void Given_SolutionWithChainDependencies_When_Converted_Then_CMakeListsProjectsAreOrdered()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(Path.Combine("LibC", "LibC.vcxproj"), new(CreateProject("StaticLibrary")));
            fileSystem.AddFile(Path.Combine("LibB", "LibB.vcxproj"), new(CreateProject("StaticLibrary", "..\\LibC\\LibC.vcxproj")));
            fileSystem.AddFile(Path.Combine("LibA", "LibA.vcxproj"), new(CreateProject("StaticLibrary", "..\\LibB\\LibB.vcxproj")));

            fileSystem.AddFile("Solution.sln", new("""
                Microsoft Visual Studio Solution File, Format Version 12.00
                Project("{GUID}") = "LibA", "LibA\LibA.vcxproj", "{AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA}"
                EndProject
                Project("{GUID}") = "LibC", "LibC\LibC.vcxproj", "{CCCCCCCC-CCCC-CCCC-CCCC-CCCCCCCCCCCC}"
                EndProject
                Project("{GUID}") = "LibB", "LibB\LibB.vcxproj", "{BBBBBBBB-BBBB-BBBB-BBBB-BBBBBBBBBBBB}"
                EndProject
            """));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(solutionFile: new("Solution.sln"));

            AssertEx.FileHasContent("CMakeLists.txt", fileSystem, """
                cmake_minimum_required(VERSION 3.24)
                project(Solution)

                add_subdirectory(LibC)
                add_subdirectory(LibB)
                add_subdirectory(LibA)
                """);
        }

        [Fact]
        public void Given_SolutionWithBranchingDependencies_When_Converted_Then_CMakeListsProjectsAreOrdered()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(Path.Combine("LibC", "LibC.vcxproj"), new(CreateProject("StaticLibrary")));
            fileSystem.AddFile(Path.Combine("LibB", "LibB.vcxproj"), new(CreateProject("StaticLibrary", "..\\LibC\\LibC.vcxproj")));
            fileSystem.AddFile(Path.Combine("LibA", "LibA.vcxproj"), new(CreateProject("StaticLibrary", "..\\LibC\\LibC.vcxproj")));
            fileSystem.AddFile(Path.Combine("App", "App.vcxproj"), new(CreateProject("Application", "..\\LibA\\LibA.vcxproj", "..\\LibB\\LibB.vcxproj")));

            fileSystem.AddFile("Branching.sln", new("""
                Microsoft Visual Studio Solution File, Format Version 12.00
                Project("{GUID}") = "App", "App\App.vcxproj", "{11111111-1111-1111-1111-111111111111}"
                EndProject
                Project("{GUID}") = "LibC", "LibC\LibC.vcxproj", "{33333333-3333-3333-3333-333333333333}"
                EndProject
                Project("{GUID}") = "LibB", "LibB\LibB.vcxproj", "{22222222-2222-2222-2222-222222222222}"
                EndProject
                Project("{GUID}") = "LibA", "LibA\LibA.vcxproj", "{44444444-4444-4444-4444-444444444444}"
                EndProject
            """));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(solutionFile: new("Branching.sln"));

            AssertEx.FileHasContent("CMakeLists.txt", fileSystem, """
                cmake_minimum_required(VERSION 3.24)
                project(Branching)

                add_subdirectory(LibC)
                add_subdirectory(LibA)
                add_subdirectory(LibB)
                add_subdirectory(App)
                """);
        }
    }
}
