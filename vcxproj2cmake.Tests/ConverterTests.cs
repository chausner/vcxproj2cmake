using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Scriban.Syntax;
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
                projectFiles: [new(@"EmptyProject.vcxproj")],
                solutionFile: null,
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
                    projectFiles: null,
                    solutionFile: new(@"EmptySolution.sln"),
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

            fileSystem.AddFile(Path.Combine("EmptyProject1", "EmptyProject1.vcxproj"), new(TestData.EmptyProject));
            fileSystem.AddFile(Path.Combine("EmptyProject2", "EmptyProject2.vcxproj"), new(TestData.EmptyProject));

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
                projectFiles: null,
                solutionFile: new(@"TwoEmptyProjects.sln"),
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

            AssertEx.FileHasContent(Path.Combine("EmptyProject1", "CMakeLists.txt"), fileSystem, """
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

            AssertEx.FileHasContent(Path.Combine("EmptyProject2", "CMakeLists.txt"), fileSystem, """
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

    public class EnableStandaloneProjectBuildsTests
    {
        [Fact]
        public void Given_ProjectReferencesAnotherProject_When_ConvertedWithEnableStandaloneProjectBuilds_Then_ProjectFileContainsAddSubdirectory()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Lib/Lib.vcxproj", new(TestData.CreateProject("Lib", "StaticLibrary")));
            fileSystem.AddFile(@"App/App.vcxproj", new(TestData.CreateProject("App", "Application", "..\\Lib\\Lib.vcxproj")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"App/App.vcxproj"), new(@"Lib/Lib.vcxproj")],
                solutionFile: null,
                qtVersion: null,
                enableStandaloneProjectBuilds: true,
                indentStyle: "spaces",
                indentSize: 4,
                dryRun: false);

            // Assert
            AssertEx.FileHasContent(@"App/CMakeLists.txt", fileSystem, """
                cmake_minimum_required(VERSION 3.13)
                project(App)

                if(NOT TARGET Lib)
                    add_subdirectory(../Lib "${CMAKE_BINARY_DIR}/Lib")
                endif()


                add_executable(App
                )

                target_link_libraries(App
                    PUBLIC
                        Lib
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
                projectFiles: [new(@"EmptyProject.vcxproj")],
                solutionFile: null,
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
                projectFiles: [new(@"EmptyProject.vcxproj")],
                solutionFile: null,
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

            fileSystem.AddFile(Path.Combine("EmptyProject1", "EmptyProject1.vcxproj"), new(TestData.EmptyProject));
            fileSystem.AddFile(Path.Combine("EmptyProject2", "EmptyProject2.vcxproj"), new(TestData.EmptyProject));

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
                projectFiles: null,
                solutionFile: new(@"TwoEmptyProjects.sln"),
                qtVersion: null,
                enableStandaloneProjectBuilds: false,
                indentStyle: "spaces",
                indentSize: 4,
                dryRun: true);

            // Assert
            var logs = string.Join(Environment.NewLine, logger.Messages);
            Assert.Contains($"Generated output for {Path.GetFullPath(Path.Combine("EmptyProject1", "CMakeLists.txt"))}", logs);
            Assert.Contains("    add_executable(EmptyProject1", logs);
            Assert.Contains($"Generated output for {Path.GetFullPath(Path.Combine("EmptyProject2", "CMakeLists.txt"))}", logs);
            Assert.Contains("    add_executable(EmptyProject2", logs);
            Assert.Contains($"Generated output for {Path.GetFullPath("CMakeLists.txt")}", logs);
            Assert.Contains("    add_subdirectory(EmptyProject1)", logs);
            Assert.Contains("    add_subdirectory(EmptyProject2)", logs);

            // Ensure no files were written to disk
            Assert.False(fileSystem.FileExists("CMakeLists.txt"));
            Assert.False(fileSystem.FileExists(Path.Combine("EmptyProject1", "CMakeLists.txt")));
            Assert.False(fileSystem.FileExists(Path.Combine("EmptyProject2", "CMakeLists.txt")));
        }
    }

    public class ValidateFoldersTests
    {
        [Fact]
        public void Given_TwoProjectsInSameDirectory_When_Converted_Then_Throws()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(Path.Combine("App", "Project1.vcxproj"), new(TestData.EmptyProject));
            fileSystem.AddFile(Path.Combine("App", "Project2.vcxproj"), new(TestData.EmptyProject));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act & Assert
            var ex = Assert.Throws<CatastrophicFailureException>(() =>
                converter.Convert(
                    projectFiles: [new(Path.Combine("App", "Project1.vcxproj")), new(Path.Combine("App", "Project2.vcxproj"))],
                    solutionFile: null,
                    qtVersion: null,
                    enableStandaloneProjectBuilds: false,
                    indentStyle: "spaces",
                    indentSize: 4,
                    dryRun: false));
            Assert.Contains("contains two or more projects", ex.Message);
            Assert.Contains(Path.GetFullPath("App"), ex.Message);
        }

        [Fact]
        public void Given_SolutionFileAndProjectInSameDirectory_When_Converted_Then_Throws()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile("Project.vcxproj", new(TestData.EmptyProject));
            fileSystem.AddFile("Solution.sln", new("""
                Microsoft Visual Studio Solution File, Format Version 12.00
                # Visual Studio Version 17
                # MinimumVisualStudioVersion = 10.0.40219.1
                Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Project", "Project.vcxproj", "{11111111-1111-1111-1111-111111111111}"
                EndProject
            """));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act & Assert
            var ex = Assert.Throws<CatastrophicFailureException>(() =>
                converter.Convert(
                    projectFiles: null,
                    solutionFile: new("Solution.sln"),
                    qtVersion: null,
                    enableStandaloneProjectBuilds: false,
                    indentStyle: "spaces",
                    indentSize: 4,
                    dryRun: false));
            Assert.Contains("The solution file and at least one project file are located in the same directory", ex.Message);
        }
    }

    public class EnsureProjectNamesAreUniqueTests
    {
        [Fact]
        public void Given_SolutionWithDuplicateProjectNames_When_Converted_Then_ProjectsGetUniqueNames()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            // Three projects with the same name in different folders
            fileSystem.AddFile(Path.Combine("Lib", "Project.vcxproj"), new(TestData.EmptyProject));
            fileSystem.AddFile(Path.Combine("App", "Project.vcxproj"), new(TestData.EmptyProject));
            fileSystem.AddFile(Path.Combine("Test", "Project.vcxproj"), new(TestData.EmptyProject));

            fileSystem.AddFile("DuplicateNames.sln", new("""
                Microsoft Visual Studio Solution File, Format Version 12.00
                Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Project", "Lib\Project.vcxproj", "{11111111-1111-1111-1111-111111111111}"
                EndProject
                Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Project", "App\Project.vcxproj", "{22222222-2222-2222-2222-222222222222}"
                EndProject
                Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Project", "Test\Project.vcxproj", "{33333333-3333-3333-3333-333333333333}"
                EndProject
            """));

            var logger = new InMemoryLogger();
            var converter = new Converter(fileSystem, logger);

            // Act
            converter.Convert(
                projectFiles: null,
                solutionFile: new("DuplicateNames.sln"),
                qtVersion: null,
                enableStandaloneProjectBuilds: false,
                indentStyle: "spaces",
                indentSize: 4,
                dryRun: true);

            // Assert
            Assert.Contains("project(Project)", logger.AllMessageText);
            Assert.Contains("project(Project2)", logger.AllMessageText);
            Assert.Contains("project(Project3)", logger.AllMessageText);
        }

        [Fact]
        public void Given_SolutionWithDuplicateProjectNamesAndNonTopLevel_When_ConvertedWithStandaloneBuilds_Then_BinaryDirsUseUniqueNames()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            // Three projects with the same name in different folders
            fileSystem.AddFile(Path.Combine("Lib", "Project.vcxproj"), new(TestData.EmptyProject));
            fileSystem.AddFile(Path.Combine("App", "Project.vcxproj"), new(TestData.EmptyProject));
            fileSystem.AddFile(Path.Combine("Test", "Project.vcxproj"), new(TestData.EmptyProject));

            fileSystem.AddFile(Path.Combine("Solution", "DuplicateNames.sln"), new("""
                Microsoft Visual Studio Solution File, Format Version 12.00
                Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Project"", "..\Lib\Project.vcxproj", "{11111111-1111-1111-1111-111111111111}"
                EndProject
                Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Project"", "..\App\Project.vcxproj", "{22222222-2222-2222-2222-222222222222}"
                EndProject
                Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Project", "..\Test\Project.vcxproj", "{33333333-3333-3333-3333-333333333333}"
                EndProject
            """));

            var logger = new InMemoryLogger();
            var converter = new Converter(fileSystem, logger);

            // Act
            converter.Convert(
                projectFiles: null,
                solutionFile: new(Path.Combine("Solution", "DuplicateNames.sln")),
                qtVersion: null,
                enableStandaloneProjectBuilds: true,
                indentStyle: "spaces",
                indentSize: 4,
                dryRun: true);

            // Assert
            Assert.Contains("""add_subdirectory(../Lib "${CMAKE_BINARY_DIR}/Project")""", logger.AllMessageText);
            Assert.Contains("""add_subdirectory(../App "${CMAKE_BINARY_DIR}/Project2")""", logger.AllMessageText);
            Assert.Contains("""add_subdirectory(../Test "${CMAKE_BINARY_DIR}/Project3")""", logger.AllMessageText);
        }
    }

    public class LanguageDetectionTests
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
            <PropertyGroup>
                <ConfigurationType>Application</ConfigurationType>
            </PropertyGroup>
            {(sources.Length > 0 ? $"""
            <ItemGroup>
                {string.Join(Environment.NewLine, sources.Select(s => $"                <ClCompile Include=\"{s}\" />"))}
            </ItemGroup>
            """ : string.Empty)}
        </Project>
        """;

        [Fact]
        public void Given_ProjectWithoutSources_When_Converted_Then_NoLanguagesWritten()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProject()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")],
                solutionFile: null,
                qtVersion: null,
                enableStandaloneProjectBuilds: false,
                indentStyle: "spaces",
                indentSize: 4,
                dryRun: false);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("project(Project)", cmake);
        }

        [Fact]
        public void Given_ProjectWithOnlyCFiles_When_Converted_Then_LanguageIsC()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProject("main.c")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")],
                solutionFile: null,
                qtVersion: null,
                enableStandaloneProjectBuilds: false,
                indentStyle: "spaces",
                indentSize: 4,
                dryRun: false);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("project(Project LANGUAGES C)", cmake);
        }

        [Fact]
        public void Given_ProjectWithOnlyCppFiles_When_Converted_Then_LanguageIsCxx()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProject("main.cpp")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")],
                solutionFile: null,
                qtVersion: null,
                enableStandaloneProjectBuilds: false,
                indentStyle: "spaces",
                indentSize: 4,
                dryRun: false);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("project(Project LANGUAGES CXX)", cmake);
        }

        [Fact]
        public void Given_ProjectWithCandCppFiles_When_Converted_Then_LanguagesAreCandCxx()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProject("main.c", "main.cpp")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")],
                solutionFile: null,
                qtVersion: null,
                enableStandaloneProjectBuilds: false,
                indentStyle: "spaces",
                indentSize: 4,
                dryRun: false);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("project(Project LANGUAGES C CXX)", cmake);
        }
    }

    public class ResolveProjectReferencesTests
    {
        [Fact]
        public void Given_ProjectReferencesUnknownProject_When_Converted_Then_Throws()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(Path.Combine("App", "App.vcxproj"), new(TestData.CreateProject("App", "Application", "..\\Missing\\Missing.vcxproj")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act & Assert
            var ex = Assert.Throws<CatastrophicFailureException>(() =>
                converter.Convert(
                    projectFiles: [new(Path.Combine("App", "App.vcxproj"))],
                    solutionFile: null,
                    qtVersion: null,
                    enableStandaloneProjectBuilds: false,
                    indentStyle: "spaces",
                    indentSize: 4,
                    dryRun: false));
            Assert.Matches(@"Project .+ references project .+ which is not part of the solution or the list of projects\.", ex.Message);
        }

        [Fact]
        public void Given_ProjectReferencesAnotherProjectWithRelativePath_When_Converted_Then_MatchesExpectedOutput()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(Path.Combine("Lib", "Lib.vcxproj"), new(TestData.CreateProject("Lib", "StaticLibrary"))); 
            fileSystem.AddFile(Path.Combine("App", "App.vcxproj"), new(TestData.CreateProject("App", "Application", "..\\Lib\\Lib.vcxproj")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(Path.Combine("App", "App.vcxproj")), new(Path.Combine("Lib", "Lib.vcxproj"))],
                solutionFile: null,
                qtVersion: null,
                enableStandaloneProjectBuilds: false,
                indentStyle: "spaces",
                indentSize: 4,
                dryRun: false);

            // Assert
            AssertEx.FileHasContent(Path.Combine("Lib", "CMakeLists.txt"), fileSystem, """
                cmake_minimum_required(VERSION 3.13)
                project(Lib)


                add_library(Lib STATIC
                )
                """);

            AssertEx.FileHasContent(Path.Combine("App", "CMakeLists.txt"), fileSystem, """
                cmake_minimum_required(VERSION 3.13)
                project(App)


                add_executable(App
                )

                target_link_libraries(App
                    PUBLIC
                        Lib
                )
                """);
        }

        [Fact]
        public void Given_ProjectReferencesAnotherProjectWithAbsolutePath_When_Converted_Then_MatchesExpectedOutput()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            var absoluteLibPath = Path.GetFullPath(Path.Combine("Lib", "Lib.vcxproj"));
            fileSystem.AddFile(Path.Combine("Lib", "Lib.vcxproj"), new(TestData.CreateProject("Lib", "StaticLibrary")));
            fileSystem.AddFile(Path.Combine("App", "App.vcxproj"), new(TestData.CreateProject("App", "Application", absoluteLibPath)));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(Path.Combine("App", "App.vcxproj")), new(Path.Combine("Lib", "Lib.vcxproj"))],
                solutionFile: null,
                qtVersion: null,
                enableStandaloneProjectBuilds: false,
                indentStyle: "spaces",
                indentSize: 4,
                dryRun: false);

            // Assert
            AssertEx.FileHasContent(Path.Combine("Lib", "CMakeLists.txt"), fileSystem, """
                cmake_minimum_required(VERSION 3.13)
                project(Lib)


                add_library(Lib STATIC
                )
                """);

            AssertEx.FileHasContent(Path.Combine("App", "CMakeLists.txt"), fileSystem, """
                cmake_minimum_required(VERSION 3.13)
                project(App)


                add_executable(App
                )

                target_link_libraries(App
                    PUBLIC
                        Lib
                )
                """);
        }
    }

    public class QtTests
    {
        static string CreateQtProject(string modules, bool moc = false, bool uic = false, bool rcc = false) => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <Project DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
            <ItemGroup Label="ProjectConfigurations">
                <ProjectConfiguration Include="Debug|Win32">
                    <Configuration>Debug</Configuration>
                    <Platform>Win32</Platform>
                </ProjectConfiguration>
            </ItemGroup>
            <PropertyGroup>
                <ConfigurationType>Application</ConfigurationType>
                <QtModules>{modules}</QtModules>
            </PropertyGroup>
            <ItemGroup>
                {(moc ? "<QtMoc Include=\"moc.h\" />" : string.Empty)}
                {(uic ? "<QtUic Include=\"form.ui\" />" : string.Empty)}
                {(rcc ? "<QtRcc Include=\"res.qrc\" />" : string.Empty)}
            </ItemGroup>
        </Project>
        """;

        [Fact]
        public void Given_QtProjectWithoutQtVersion_When_Converted_Then_Throws()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"QtProject.vcxproj", new(CreateQtProject("core", true, true, true)));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            var ex = Assert.Throws<CatastrophicFailureException>(() =>
                converter.Convert(
                    projectFiles: [new(@"QtProject.vcxproj")],
                    solutionFile: null,
                    qtVersion: null,
                    enableStandaloneProjectBuilds: false,
                    indentStyle: "spaces",
                    indentSize: 4,
                    dryRun: false));

            Assert.Equal("Project uses Qt but no Qt version is set. Specify the version with --qt-version.", ex.Message);
        }

        [Fact]
        public void Given_QtProjectWithUnknownModule_When_Converted_Then_Throws()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"QtProject.vcxproj", new(CreateQtProject("doesnotexist")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            var ex = Assert.Throws<CatastrophicFailureException>(() =>
                converter.Convert(
                    projectFiles: [new(@"QtProject.vcxproj")],
                    solutionFile: null,
                    qtVersion: 6,
                    enableStandaloneProjectBuilds: false,
                    indentStyle: "spaces",
                    indentSize: 4,
                    dryRun: false));

            Assert.Contains("Unknown Qt module", ex.Message);
        }

        [Fact]
        public void Given_QtProjectWithQtVersionAndModules_When_Converted_Then_MatchesExpectedOutput()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"QtProject.vcxproj", new(CreateQtProject("core;widgets", true, true, true)));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"QtProject.vcxproj")],
                solutionFile: null,
                qtVersion: 6,
                enableStandaloneProjectBuilds: false,
                indentStyle: "spaces",
                indentSize: 4,
                dryRun: false);

            AssertEx.FileHasContent(@"CMakeLists.txt", fileSystem, """
            cmake_minimum_required(VERSION 3.13)
            project(QtProject)

            find_package(Qt6 REQUIRED COMPONENTS Core Widgets)

            add_executable(QtProject
                form.ui
                res.qrc
            )

            set_target_properties(QtProject PROPERTIES
                AUTOMOC ON
                AUTOUIC ON
                AUTORCC ON
            )

            target_link_libraries(QtProject
                PUBLIC
                    Qt6::Core
                    Qt6::Widgets
            )
            """);
        }
    }

    public class RemoveObsoleteLibrariesFromProjectReferencesTests
    {
        static string CreateAppProject(bool linkLibraryDependencies) => $"""
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
                <ItemGroup>
                    <ProjectReference Include="..\\Lib\\Lib.vcxproj" />
                </ItemGroup>
                <ItemDefinitionGroup>
                    <ProjectReference>
                        <LinkLibraryDependencies>{(linkLibraryDependencies ? "true" : "false")}</LinkLibraryDependencies>
                    </ProjectReference>
                    <Link>
                        <AdditionalDependencies>Lib.lib;%(AdditionalDependencies)</AdditionalDependencies>
                    </Link>
                </ItemDefinitionGroup>
            </Project>
            """;

        [Fact]
        public void Given_ProjectLinksLibraryExplicitlyAndLinkLibraryDependenciesDisabled_When_Converted_Then_LibraryIsPreserved()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Lib/Lib.vcxproj", new(TestData.CreateProject("Lib", "StaticLibrary")));
            fileSystem.AddFile(@"App/App.vcxproj", new(CreateAppProject(false)));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"App/App.vcxproj"), new(@"Lib/Lib.vcxproj")],
                solutionFile: null,
                qtVersion: null,
                enableStandaloneProjectBuilds: false,
                indentStyle: "spaces",
                indentSize: 4,
                dryRun: false);

            // Assert
            AssertEx.FileHasContent(@"App/CMakeLists.txt", fileSystem, """
                cmake_minimum_required(VERSION 3.13)
                project(App)


                add_executable(App
                )

                target_link_libraries(App
                    PUBLIC
                        Lib.lib
                )
                """);
        }

        [Fact]
        public void Given_ProjectLinksLibraryExplicitlyAndLinkLibraryDependenciesEnabled_When_Converted_Then_LibraryIsRemovedAndLogged()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Lib/Lib.vcxproj", new(TestData.CreateProject("Lib", "StaticLibrary")));
            fileSystem.AddFile(@"App/App.vcxproj", new(CreateAppProject(true)));

            var logger = new InMemoryLogger();
            var converter = new Converter(fileSystem, logger);

            // Act
            converter.Convert(
                projectFiles: [new(@"App/App.vcxproj"), new(@"Lib/Lib.vcxproj")],
                solutionFile: null,
                qtVersion: null,
                enableStandaloneProjectBuilds: false,
                indentStyle: "spaces",
                indentSize: 4,
                dryRun: false);

            // Assert
            AssertEx.FileHasContent(@"App/CMakeLists.txt", fileSystem, """
                cmake_minimum_required(VERSION 3.13)
                project(App)


                add_executable(App
                )

                target_link_libraries(App
                    PUBLIC
                        Lib
                )
                """);

            Assert.Contains(
                "Removing explicit library dependency Lib.lib from project App since LinkLibraryDependencies is enabled.",
                logger.AllMessageText);
        }

    }

    public class ConfigurationTypeTests
    {
        [Fact]
        public void Given_ApplicationProject_When_Converted_Then_UsesAddExecutable()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"App.vcxproj", new(TestData.CreateProject("App", "Application")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"App.vcxproj")],
                solutionFile: null,
                qtVersion: null,
                enableStandaloneProjectBuilds: false,
                indentStyle: "spaces",
                indentSize: 4,
                dryRun: false);

            AssertEx.FileHasContent(@"CMakeLists.txt", fileSystem, """
                cmake_minimum_required(VERSION 3.13)
                project(App)


                add_executable(App
                )

                """);
        }

        [Fact]
        public void Given_StaticLibraryProject_When_Converted_Then_UsesAddLibraryStatic()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Lib.vcxproj", new(TestData.CreateProject("Lib", "StaticLibrary")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"Lib.vcxproj")],
                solutionFile: null,
                qtVersion: null,
                enableStandaloneProjectBuilds: false,
                indentStyle: "spaces",
                indentSize: 4,
                dryRun: false);

            AssertEx.FileHasContent(@"CMakeLists.txt", fileSystem, """
                cmake_minimum_required(VERSION 3.13)
                project(Lib)


                add_library(Lib STATIC
                )
                """);
        }

        [Fact]
        public void Given_DynamicLibraryProject_When_Converted_Then_UsesAddLibraryShared()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Dll.vcxproj", new(TestData.CreateProject("Dll", "DynamicLibrary")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"Dll.vcxproj")],
                solutionFile: null,
                qtVersion: null,
                enableStandaloneProjectBuilds: false,
                indentStyle: "spaces",
                indentSize: 4,
                dryRun: false);

            AssertEx.FileHasContent(@"CMakeLists.txt", fileSystem, """
                cmake_minimum_required(VERSION 3.13)
                project(Dll)


                add_library(Dll SHARED
                )
                """);
        }

        [Fact]
        public void Given_HeaderOnlyLibrary_When_Converted_Then_UsesAddLibraryInterface()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"HeaderOnly.vcxproj", new("""
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
                    </PropertyGroup>
                    <ItemGroup>
                        <ClInclude Include="header.hpp" />
                    </ItemGroup>
                </Project>
                """));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"HeaderOnly.vcxproj")],
                solutionFile: null,
                qtVersion: null,
                enableStandaloneProjectBuilds: false,
                indentStyle: "spaces",
                indentSize: 4,
                dryRun: false);

            AssertEx.FileHasContent(@"CMakeLists.txt", fileSystem, """
                cmake_minimum_required(VERSION 3.13)
                project(HeaderOnly)


                add_library(HeaderOnly INTERFACE)
                """);
        }

        [Fact]
        public void Given_ProjectWithUnsupportedConfigurationType_When_Converted_Then_Throws()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Bad.vcxproj", new(TestData.CreateProject("Bad", "Makefile")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            var ex = Assert.Throws<ScriptRuntimeException>(() =>
                converter.Convert(
                    projectFiles: [new(@"Bad.vcxproj")],
                    solutionFile: null,
                    qtVersion: null,
                    enableStandaloneProjectBuilds: false,
                    indentStyle: "spaces",
                    indentSize: 4,
                    dryRun: false));

            Assert.Contains("Unsupported configuration type", ex.Message);
        }
    }

    public class AdditionalDependenciesTests
    {
        [Fact]
        public void Given_ProjectWithAdditionalDependencies_When_Converted_Then_LibrariesAreLinked()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new("""
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
                            <AdditionalDependencies>Foo.lib;Bar.lib;%(AdditionalDependencies)</AdditionalDependencies>
                        </Link>
                    </ItemDefinitionGroup>
                </Project>
                """));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")],
                solutionFile: null,
                qtVersion: null,
                enableStandaloneProjectBuilds: false,
                indentStyle: "spaces",
                indentSize: 4,
                dryRun: false);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains(
                """
                target_link_libraries(Project
                    PUBLIC
                        Foo.lib
                        Bar.lib
                )
                """.Trim(),
                cmake);
        }

        [Fact]
        public void Given_ProjectWithConfigSpecificAdditionalDependencies_When_Converted_Then_GeneratorExpressionsUsed()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new("""
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
                            <AdditionalDependencies>Foo_d.lib;%(AdditionalDependencies)</AdditionalDependencies>
                        </Link>
                    </ItemDefinitionGroup>
                    <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Release|Win32'">
                        <Link>
                            <AdditionalDependencies>Foo.lib;%(AdditionalDependencies)</AdditionalDependencies>
                        </Link>
                    </ItemDefinitionGroup>
                </Project>
                """));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")],
                solutionFile: null,
                qtVersion: null,
                enableStandaloneProjectBuilds: false,
                indentStyle: "spaces",
                indentSize: 4,
                dryRun: false);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains(
                """
                target_link_libraries(Project
                    PUBLIC
                        $<$<CONFIG:Debug>:Foo_d.lib>
                        $<$<CONFIG:Release>:Foo.lib>
                )
                """.Trim(),
                cmake);
        }
    }

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
                projectFiles: [new(@"Project.vcxproj")],
                solutionFile: null,
                qtVersion: null,
                enableStandaloneProjectBuilds: false,
                indentStyle: "spaces",
                indentSize: 4,
                dryRun: false);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("find_package(OpenMP)", cmake);
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
                projectFiles: [new(@"Project.vcxproj")],
                solutionFile: null,
                qtVersion: null,
                enableStandaloneProjectBuilds: false,
                indentStyle: "spaces",
                indentSize: 4,
                dryRun: false);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("find_package(OpenMP)", cmake);
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
                projectFiles: [new(@"Project.vcxproj")],
                solutionFile: null,
                qtVersion: null,
                enableStandaloneProjectBuilds: false,
                indentStyle: "spaces",
                indentSize: 4,
                dryRun: false);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.DoesNotContain("find_package(OpenMP)", cmake);
            Assert.DoesNotContain("OpenMP::OpenMP_CXX", cmake);
            Assert.DoesNotContain("target_link_libraries(Project", cmake);
        }

        [Fact]
        public void Given_InvalidOpenMPValue_When_Converted_Then_Throws()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProject("foo", "foo")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            Assert.Throws<CatastrophicFailureException>(() => converter.Convert(
                projectFiles: [new(@"Project.vcxproj")],
                solutionFile: null,
                qtVersion: null,
                enableStandaloneProjectBuilds: false,
                indentStyle: "spaces",
                indentSize: 4,
                dryRun: false));
        }
    }

    public class ConanPackagesTests
    {
        static string CreateProjectWithConanImports(params string[] packages)
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
            <PropertyGroup>
                <ConfigurationType>Application</ConfigurationType>
            </PropertyGroup>
            {string.Join(Environment.NewLine, packages.Select(p => $"<Import Project=\"conan_{p}.props\" />"))}
        </Project>
        """;

        [Fact]
        public void Given_ProjectWithKnownConanPackage_When_Converted_Then_FindPackageAndTargetLinkLibrariesAreGenerated()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProjectWithConanImports("boost")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")],
                solutionFile: null,
                qtVersion: null,
                enableStandaloneProjectBuilds: false,
                indentStyle: "spaces",
                indentSize: 4,
                dryRun: false);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("find_package(Boost REQUIRED CONFIG)", cmake);
            Assert.Contains(
                """
                target_link_libraries(Project
                    PUBLIC
                        boost::boost
                )
                """.Trim(),
                cmake);
        }

        [Fact]
        public void Given_ProjectWithUnknownConanPackage_When_Converted_Then_DefaultNamesAreUsed()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProjectWithConanImports("unknown")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")],
                solutionFile: null,
                qtVersion: null,
                enableStandaloneProjectBuilds: false,
                indentStyle: "spaces",
                indentSize: 4,
                dryRun: false);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("find_package(unknown REQUIRED CONFIG)", cmake);
            Assert.Contains(
                """
                target_link_libraries(Project
                    PUBLIC
                        unknown::unknown
                )
                """.Trim(),
                cmake);
        }

        [Fact]
        public void Given_ProjectWithMultipleConanPackages_When_Converted_Then_AllPackagesAreLinked()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProjectWithConanImports("boost", "fmt")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")],
                solutionFile: null,
                qtVersion: null,
                enableStandaloneProjectBuilds: false,
                indentStyle: "spaces",
                indentSize: 4,
                dryRun: false);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("find_package(Boost REQUIRED CONFIG)", cmake);
            Assert.Contains("find_package(fmt REQUIRED CONFIG)", cmake);
            Assert.Contains(
                """
                target_link_libraries(Project
                    PUBLIC
                        boost::boost
                        fmt::fmt
                )
                """.Trim(),
                cmake);
        }
    }

    public class LinkerSubsystemTests
    {
        static string CreateProjectWithSubsystem(string debugSubsystem, string releaseSubsystem) => $"""
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
                        <SubSystem>{debugSubsystem}</SubSystem>
                    </Link>
                </ItemDefinitionGroup>
                <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Release|Win32'">
                    <Link>
                        <SubSystem>{releaseSubsystem}</SubSystem>
                    </Link>
                </ItemDefinitionGroup>
            </Project>
            """;

        [Fact]
        public void Given_ProjectWithWindowsSubsystem_When_Converted_Then_AddExecutableContainsWin32()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProjectWithSubsystem("Windows", "Windows")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")],
                solutionFile: null,
                qtVersion: null,
                enableStandaloneProjectBuilds: false,
                indentStyle: "spaces",
                indentSize: 4,
                dryRun: false);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("add_executable(Project WIN32", cmake);
        }

        [Fact]
        public void Given_ProjectWithConsoleSubsystem_When_Converted_Then_AddExecutableDoesNotContainWin32()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProjectWithSubsystem("Console", "Console")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")],
                solutionFile: null,
                qtVersion: null,
                enableStandaloneProjectBuilds: false,
                indentStyle: "spaces",
                indentSize: 4,
                dryRun: false);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.DoesNotContain("WIN32", cmake);
        }

        [Fact]
        public void Given_ProjectWithInconsistentSubsystem_When_Converted_Then_Throws()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProjectWithSubsystem("Windows", "Console")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            var ex = Assert.Throws<CatastrophicFailureException>(() =>
                converter.Convert(
                    projectFiles: [new(@"Project.vcxproj")],
                    solutionFile: null,
                    qtVersion: null,
                    enableStandaloneProjectBuilds: false,
                    indentStyle: "spaces",
                    indentSize: 4,
                    dryRun: false));

            Assert.Contains("SubSystem property is inconsistent between configurations", ex.Message);
        }
    }
}
