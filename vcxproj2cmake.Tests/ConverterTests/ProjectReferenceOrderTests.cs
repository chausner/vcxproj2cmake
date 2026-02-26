using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class ProjectReferenceOrderTests
    {
        [Fact]
        public void Given_SolutionWithChainDependencies_When_Converted_Then_CMakeListsProjectsAreOrdered()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(Path.Combine("LibC", "LibC.vcxproj"), new(TestData.CreateProjectWithProjectReferences(
                configurationType: "StaticLibrary",
                projectReferences: [])));
            fileSystem.AddFile(Path.Combine("LibB", "LibB.vcxproj"), new(TestData.CreateProjectWithProjectReferences(
                configurationType: "StaticLibrary",
                projectReferences: ["..\\LibC\\LibC.vcxproj"])));
            fileSystem.AddFile(Path.Combine("LibA", "LibA.vcxproj"), new(TestData.CreateProjectWithProjectReferences(
                configurationType: "StaticLibrary",
                projectReferences: ["..\\LibB\\LibB.vcxproj"])));

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

            fileSystem.AddFile(Path.Combine("LibC", "LibC.vcxproj"), new(TestData.CreateProjectWithProjectReferences(
                configurationType: "StaticLibrary",
                projectReferences: [])));
            fileSystem.AddFile(Path.Combine("LibB", "LibB.vcxproj"), new(TestData.CreateProjectWithProjectReferences(
                configurationType: "StaticLibrary",
                projectReferences: ["..\\LibC\\LibC.vcxproj"])));
            fileSystem.AddFile(Path.Combine("LibA", "LibA.vcxproj"), new(TestData.CreateProjectWithProjectReferences(
                configurationType: "StaticLibrary",
                projectReferences: ["..\\LibC\\LibC.vcxproj"])));
            fileSystem.AddFile(Path.Combine("App", "App.vcxproj"), new(TestData.CreateProjectWithProjectReferences(
                configurationType: "Application",
                projectReferences: ["..\\LibA\\LibA.vcxproj", "..\\LibB\\LibB.vcxproj"])));

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
