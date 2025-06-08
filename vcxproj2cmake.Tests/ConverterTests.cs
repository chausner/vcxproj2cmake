using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public class ConverterTests
{
    public class BasicTests
    {
        [Fact]
        public void Given_EmptyProject_When_Converted_Then_MatchesExpectedOutput()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"EmptyProject.vcxproj", new(TestData.EmptyProject));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projects: [new(@"EmptyProject.vcxproj")],
                solution: null,
                qtVersion: null,
                enableStandaloneProjectBuilds: false,
                indentStyle: "spaces",
                indentSize: 4,
                dryRun: false);

            // Assert
            AssertEx.FileHasContent(@"CMakeLists.txt", fileSystem, """            
                cmake_minimum_required(VERSION 3.13)
                project(EmptyProject)


                add_executable(EmptyProject
                )

                target_compile_definitions(EmptyProject
                    PUBLIC
                        WIN32
                        _CONSOLE
                        UNICODE
                        _UNICODE
                        $<$<CONFIG:Debug>:_DEBUG>
                        $<$<CONFIG:Release>:NDEBUG>
                )

                target_compile_options(EmptyProject
                    PUBLIC
                        /W3
                )            
                """);
        }

        [Fact]
        public void Given_EmptySolution_When_Converted_Then_Throws()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"EmptySolution.sln", new("""
                Microsoft Visual Studio Solution File, Format Version 12.00
                # Visual Studio Version 17
                # MinimumVisualStudioVersion = 10.0.40219.1
            """));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act & Assert
            var ex = Assert.Throws<CatastrophicFailureException>(() =>
                converter.Convert(
                    projects: null,
                    solution: new(@"EmptySolution.sln"),
                    qtVersion: null,
                    enableStandaloneProjectBuilds: false,
                    indentStyle: "spaces",
                    indentSize: 4,
                    dryRun: false));
            Assert.Contains("No .vcxproj files found in solution", ex.Message);
        }
    }

    public class IndentStyleAndIndentSizeTests
    {

        [Fact]
        public void Given_EmptyProject_When_ConvertedWithIndentStyleTabs_Then_MatchesExpectedOutput()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"EmptyProject.vcxproj", new(TestData.EmptyProject));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projects: [new(@"EmptyProject.vcxproj")],
                solution: null,
                qtVersion: null,
                enableStandaloneProjectBuilds: false,
                indentStyle: "tabs",
                indentSize: 4,
                dryRun: false);

            // Assert
            AssertEx.FileHasContent(@"CMakeLists.txt", fileSystem, $"""            
                cmake_minimum_required(VERSION 3.13)
                project(EmptyProject)


                add_executable(EmptyProject
                )

                target_compile_definitions(EmptyProject
                {"\t"}PUBLIC
                {"\t"}{"\t"}WIN32
                {"\t"}{"\t"}_CONSOLE
                {"\t"}{"\t"}UNICODE
                {"\t"}{"\t"}_UNICODE
                {"\t"}{"\t"}$<$<CONFIG:Debug>:_DEBUG>
                {"\t"}{"\t"}$<$<CONFIG:Release>:NDEBUG>
                )

                target_compile_options(EmptyProject
                {"\t"}PUBLIC
                {"\t"}{"\t"}/W3
                )            
                """);
        }

        [Fact]
        public void Given_EmptyProject_When_ConvertedWithIndentSize2_Then_MatchesExpectedOutput()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"EmptyProject.vcxproj", new(TestData.EmptyProject));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projects: [new(@"EmptyProject.vcxproj")],
                solution: null,
                qtVersion: null,
                enableStandaloneProjectBuilds: false,
                indentStyle: "spaces",
                indentSize: 2,
                dryRun: false);

            // Assert
            AssertEx.FileHasContent(@"CMakeLists.txt", fileSystem, """            
                cmake_minimum_required(VERSION 3.13)
                project(EmptyProject)


                add_executable(EmptyProject
                )

                target_compile_definitions(EmptyProject
                  PUBLIC
                    WIN32
                    _CONSOLE
                    UNICODE
                    _UNICODE
                    $<$<CONFIG:Debug>:_DEBUG>
                    $<$<CONFIG:Release>:NDEBUG>
                )

                target_compile_options(EmptyProject
                  PUBLIC
                    /W3
                )            
                """);
        }
    }
}
