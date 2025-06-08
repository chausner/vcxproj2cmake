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

        [Fact]
        public void Given_SolutionWithTwoEmptyProjects_When_Converted_Then_MatchesExpectedOutput()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"EmptyProject1\EmptyProject1.vcxproj", new(TestData.EmptyProject));
            fileSystem.AddFile(@"EmptyProject2\EmptyProject2.vcxproj", new(TestData.EmptyProject));

            fileSystem.AddFile(@"TwoEmptyProjects.sln", new("""                
                Microsoft Visual Studio Solution File, Format Version 12.00
                # Visual Studio Version 17
                # MinimumVisualStudioVersion = 10.0.40219.1
                Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "EmptyProject1", "EmptyProject1\EmptyProject1.vcxproj", "{11111111-1111-1111-1111-111111111111}"
                EndProject
                Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "EmptyProject2", "EmptyProject2\EmptyProject2.vcxproj", "{22222222-2222-2222-2222-222222222222}"
                EndProject
                """));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projects: null,
                solution: new(@"TwoEmptyProjects.sln"),
                qtVersion: null,
                enableStandaloneProjectBuilds: false,
                indentStyle: "spaces",
                indentSize: 4,
                dryRun: false);

            // Assert
            AssertEx.FileHasContent(@"CMakeLists.txt", fileSystem, """
                cmake_minimum_required(VERSION 3.13)
                project(TwoEmptyProjects)

                add_subdirectory(EmptyProject1)
                add_subdirectory(EmptyProject2)
                """);

            AssertEx.FileHasContent(@"EmptyProject1\CMakeLists.txt", fileSystem, """            
                cmake_minimum_required(VERSION 3.13)
                project(EmptyProject1)


                add_executable(EmptyProject1
                )

                target_compile_definitions(EmptyProject1
                    PUBLIC
                        WIN32
                        _CONSOLE
                        UNICODE
                        _UNICODE
                        $<$<CONFIG:Debug>:_DEBUG>
                        $<$<CONFIG:Release>:NDEBUG>
                )

                target_compile_options(EmptyProject1
                    PUBLIC
                        /W3
                )
                """);

            AssertEx.FileHasContent(@"EmptyProject2\CMakeLists.txt", fileSystem, """            
                cmake_minimum_required(VERSION 3.13)
                project(EmptyProject2)


                add_executable(EmptyProject2
                )

                target_compile_definitions(EmptyProject2
                    PUBLIC
                        WIN32
                        _CONSOLE
                        UNICODE
                        _UNICODE
                        $<$<CONFIG:Debug>:_DEBUG>
                        $<$<CONFIG:Release>:NDEBUG>
                )

                target_compile_options(EmptyProject2
                    PUBLIC
                        /W3
                )
                """);
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

    public class DryRunTests
    {
        [Fact]
        public void Given_SolutionWithTwoEmptyProjects_When_ConvertedWithDryRun_Then_LogsExpectedOutputAndWritesNoFiles()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"EmptyProject1\EmptyProject1.vcxproj", new(TestData.EmptyProject));
            fileSystem.AddFile(@"EmptyProject2\EmptyProject2.vcxproj", new(TestData.EmptyProject));

            fileSystem.AddFile(@"TwoEmptyProjects.sln", new("""                
                Microsoft Visual Studio Solution File, Format Version 12.00
                # Visual Studio Version 17
                # MinimumVisualStudioVersion = 10.0.40219.1
                Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "EmptyProject1", "EmptyProject1\EmptyProject1.vcxproj", "{11111111-1111-1111-1111-111111111111}"
                EndProject
                Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "EmptyProject2", "EmptyProject2\EmptyProject2.vcxproj", "{22222222-2222-2222-2222-222222222222}"
                EndProject
                """));

            var logger = new InMemoryLogger();
            var converter = new Converter(fileSystem, logger);

            // Act
            converter.Convert(
                projects: null,
                solution: new(@"TwoEmptyProjects.sln"),
                qtVersion: null,
                enableStandaloneProjectBuilds: false,
                indentStyle: "spaces",
                indentSize: 4,
                dryRun: true);

            // Assert
            var logs = string.Join(Environment.NewLine, logger.Messages);
            Assert.Contains($"Generated output for {Path.GetFullPath(@"EmptyProject1\CMakeLists.txt")}", logs);
            Assert.Contains("    add_executable(EmptyProject1", logs);
            Assert.Contains($"Generated output for {Path.GetFullPath(@"EmptyProject2\CMakeLists.txt")}", logs);
            Assert.Contains("    add_executable(EmptyProject2", logs);
            Assert.Contains($"Generated output for {Path.GetFullPath("CMakeLists.txt")}", logs);
            Assert.Contains("    add_subdirectory(EmptyProject1)", logs);
            Assert.Contains("    add_subdirectory(EmptyProject2)", logs);

            // Ensure no files were written to disk
            Assert.False(fileSystem.FileExists("CMakeLists.txt"));
            Assert.False(fileSystem.FileExists(@"EmptyProject1\CMakeLists.txt"));
            Assert.False(fileSystem.FileExists(@"EmptyProject2\CMakeLists.txt"));
        }
    }
}
