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

    public class LinkLibraryDependenciesTests
    {
        [Fact]
        public void Given_ProjectReferencesDynamicLibrary_When_Converted_Then_LibraryIsLinked()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(Path.Combine("Dll", "Dll.vcxproj"), new(TestData.CreateProject("Dll", "DynamicLibrary")));
            fileSystem.AddFile(Path.Combine("App", "App.vcxproj"), new(TestData.CreateProject("App", "Application", "..\\Dll\\Dll.vcxproj")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(Path.Combine("App", "App.vcxproj")), new(Path.Combine("Dll", "Dll.vcxproj"))],
                solutionFile: null,
                qtVersion: null,
                enableStandaloneProjectBuilds: false,
                indentStyle: "spaces",
                indentSize: 4,
                dryRun: false);

            AssertEx.FileHasContent(Path.Combine("Dll", "CMakeLists.txt"), fileSystem, """
                cmake_minimum_required(VERSION 3.13)
                project(Dll)


                add_library(Dll SHARED
                )
                """);

            AssertEx.FileHasContent(Path.Combine("App", "CMakeLists.txt"), fileSystem, """
                cmake_minimum_required(VERSION 3.13)
                project(App)


                add_executable(App
                )

                target_link_libraries(App
                    PUBLIC
                        Dll
                )
                """);
        }

        [Fact]
        public void Given_ProjectReferencesHeaderOnlyLibrary_When_Converted_Then_LibraryIsLinked()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(Path.Combine("HeaderOnly", "HeaderOnly.vcxproj"), new("""
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
            fileSystem.AddFile(Path.Combine("App", "App.vcxproj"), new(TestData.CreateProject("App", "Application", "..\\HeaderOnly\\HeaderOnly.vcxproj")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(Path.Combine("App", "App.vcxproj")), new(Path.Combine("HeaderOnly", "HeaderOnly.vcxproj"))],
                solutionFile: null,
                qtVersion: null,
                enableStandaloneProjectBuilds: false,
                indentStyle: "spaces",
                indentSize: 4,
                dryRun: false);

            AssertEx.FileHasContent(Path.Combine("HeaderOnly", "CMakeLists.txt"), fileSystem, """
                cmake_minimum_required(VERSION 3.13)
                project(HeaderOnly)


                add_library(HeaderOnly INTERFACE)
                """);

            AssertEx.FileHasContent(Path.Combine("App", "CMakeLists.txt"), fileSystem, """
                cmake_minimum_required(VERSION 3.13)
                project(App)


                add_executable(App
                )

                target_link_libraries(App
                    PUBLIC
                        HeaderOnly
                )
                """);
        }

        [Fact]
        public void Given_ProjectReferencesApplication_When_Converted_Then_NoLibraryIsLinked()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(Path.Combine("Exe", "Exe.vcxproj"), new(TestData.CreateProject("Exe", "Application")));
            fileSystem.AddFile(Path.Combine("App", "App.vcxproj"), new(TestData.CreateProject("App", "Application", "..\\Exe\\Exe.vcxproj")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(Path.Combine("App", "App.vcxproj")), new(Path.Combine("Exe", "Exe.vcxproj"))],
                solutionFile: null,
                qtVersion: null,
                enableStandaloneProjectBuilds: false,
                indentStyle: "spaces",
                indentSize: 4,
                dryRun: false);

            var cmake = fileSystem.GetFile(Path.Combine("App", "CMakeLists.txt")).TextContents;
            Assert.DoesNotContain("target_link_libraries(App", cmake);
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
                projectFiles: [new(@"Project.vcxproj")],
                solutionFile: null,
                qtVersion: null,
                enableStandaloneProjectBuilds: false,
                indentStyle: "spaces",
                indentSize: 4,
                dryRun: false);

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
                projectFiles: [new(@"Project.vcxproj")],
                solutionFile: null,
                qtVersion: null,
                enableStandaloneProjectBuilds: false,
                indentStyle: "spaces",
                indentSize: 4,
                dryRun: false);

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
                projectFiles: [new(@"Project.vcxproj")],
                solutionFile: null,
                qtVersion: null,
                enableStandaloneProjectBuilds: false,
                indentStyle: "spaces",
                indentSize: 4,
                dryRun: false);

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
                projectFiles: [new(@"Project.vcxproj")],
                solutionFile: null,
                qtVersion: null,
                enableStandaloneProjectBuilds: false,
                indentStyle: "spaces",
                indentSize: 4,
                dryRun: false);

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
                projectFiles: [new(@"Project.vcxproj")],
                solutionFile: null,
                qtVersion: null,
                enableStandaloneProjectBuilds: false,
                indentStyle: "spaces",
                indentSize: 4,
                dryRun: false));

            Assert.Contains("Invalid value for AllProjectIncludesArePublic", ex.Message);
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

    public class PrecompiledHeaderTests
    {
        static string CreateProject(
            string debugMode,
            string releaseMode,
            string debugHeader = "pch.h",
            string releaseHeader = "pch.h") => $"""
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
                        <PrecompiledHeader>{debugMode}</PrecompiledHeader>
                        <PrecompiledHeaderFile>{debugHeader}</PrecompiledHeaderFile>
                    </ClCompile>
                </ItemDefinitionGroup>
                <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Release|Win32'">
                    <ClCompile>
                        <PrecompiledHeader>{releaseMode}</PrecompiledHeader>
                        <PrecompiledHeaderFile>{releaseHeader}</PrecompiledHeaderFile>
                    </ClCompile>
                </ItemDefinitionGroup>
            </Project>
            """;

        [Fact]
        public void Given_PrecompiledHeaderUsedInAllConfigs_When_Converted_Then_TargetPrecompileHeadersAdded()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProject("Use", "Use")));

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
                target_precompile_headers(Project
                    PRIVATE
                        ${CMAKE_CURRENT_SOURCE_DIR}/pch.h
                )
                """,
                cmake);
        }

        [Fact]
        public void Given_PrecompiledHeaderUsedOnlyForDebug_When_Converted_Then_GeneratorExpressionWritten()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProject("Use", "NotUsing")));

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
                target_precompile_headers(Project
                    PRIVATE
                        $<$<CONFIG:Debug>:${CMAKE_CURRENT_SOURCE_DIR}/pch.h>
                )
                """,
                cmake);
        }

        [Fact]
        public void Given_PrecompiledHeaderUsesDifferentFilesPerConfig_When_Converted_Then_BothHeadersWritten()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProject("Use", "Use", "pch_debug.h", "pch_release.h")));

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
                target_precompile_headers(Project
                    PRIVATE
                        $<$<CONFIG:Debug>:${CMAKE_CURRENT_SOURCE_DIR}/pch_debug.h>
                        $<$<CONFIG:Release>:${CMAKE_CURRENT_SOURCE_DIR}/pch_release.h>
                )
                """,
                cmake);
        }

        [Fact]
        public void Given_PrecompiledHeaderDisabled_When_Converted_Then_NoPrecompileHeaderBlock()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProject("NotUsing", "NotUsing")));

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
            Assert.DoesNotContain("target_precompile_headers(Project", cmake);
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

    public class LanguageStandardTests
    {
        static string CreateProject(string? cppDebug = null, string? cppRelease = null, string? cDebug = null, string? cRelease = null) => $"""
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
                        {(cppDebug != null ? $"<LanguageStandard>{cppDebug}</LanguageStandard>" : string.Empty)}
                        {(cDebug != null ? $"<LanguageStandard_C>{cDebug}</LanguageStandard_C>" : string.Empty)}
                    </ClCompile>
                </ItemDefinitionGroup>
                <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Release|Win32'">
                    <ClCompile>
                        {(cppRelease != null ? $"<LanguageStandard>{cppRelease}</LanguageStandard>" : string.Empty)}
                        {(cRelease != null ? $"<LanguageStandard_C>{cRelease}</LanguageStandard_C>" : string.Empty)}
                    </ClCompile>
                </ItemDefinitionGroup>
            </Project>
            """;

        [Theory]
        [InlineData("stdcpplatest", "cxx_std_23")]
        [InlineData("stdcpp23", "cxx_std_23")]
        [InlineData("stdcpp20", "cxx_std_20")]
        [InlineData("stdcpp17", "cxx_std_17")]
        [InlineData("stdcpp14", "cxx_std_14")]
        [InlineData("stdcpp11", "cxx_std_11")]
        [InlineData("Default", null)]
        public void Given_ProjectWithCppStandard_When_Converted_Then_FeaturesMatch(string standard, string? expected)
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProject(standard, standard)));

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

            if (expected != null)
                Assert.Contains($"target_compile_features(Project PUBLIC {expected})", cmake);
            else
                Assert.DoesNotContain("target_compile_features", cmake);
        }

        [Theory]
        [InlineData("stdclatest", "c_std_23")]
        [InlineData("stdc23", "c_std_23")]
        [InlineData("stdc17", "c_std_17")]
        [InlineData("stdc11", "c_std_11")]
        [InlineData("Default", null)]
        public void Given_ProjectWithCStandard_When_Converted_Then_FeaturesMatch(string standard, string? expected)
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProject(null, null, standard, standard)));

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

            if (expected != null)
                Assert.Contains($"target_compile_features(Project PUBLIC {expected})", cmake);
            else
                Assert.DoesNotContain("target_compile_features", cmake);
        }

        [Fact]
        public void Given_ProjectWithCpp17AndC11_When_Converted_Then_TargetCompileFeaturesContainsBoth()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProject("stdcpp17", "stdcpp17", "stdc11", "stdc11")));

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
            Assert.Contains("target_compile_features(Project PUBLIC cxx_std_17 c_std_11)", cmake);
        }

        [Fact]
        public void Given_ProjectWithDefaultStandards_When_Converted_Then_NoTargetCompileFeaturesGenerated()
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
            Assert.DoesNotContain("target_compile_features", cmake);
        }

        [Fact]
        public void Given_InconsistentCppStandards_When_Converted_Then_Throws()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProject("stdcpp20", "stdcpp17")));

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

            Assert.Contains("LanguageStandard property is inconsistent between configurations", ex.Message);
        }

        [Fact]
        public void Given_InconsistentCStandards_When_Converted_Then_Throws()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProject(null, null, "stdc17", "stdc11")));

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

            Assert.Contains("LanguageStandard_C property is inconsistent between configurations", ex.Message);
        }

        [Fact]
        public void Given_UnsupportedCppStandard_When_Converted_Then_Throws()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProject("foo", "foo")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            var ex = Assert.Throws<ScriptRuntimeException>(() =>
                converter.Convert(
                    projectFiles: [new(@"Project.vcxproj")],
                    solutionFile: null,
                    qtVersion: null,
                    enableStandaloneProjectBuilds: false,
                    indentStyle: "spaces",
                    indentSize: 4,
                    dryRun: false));

            Assert.Contains("Unsupported C++ language standard", ex.Message);
        }

        [Fact]
        public void Given_UnsupportedCStandard_When_Converted_Then_Throws()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProject(null, null, "foo", "foo")));    

            var converter = new Converter(fileSystem, NullLogger.Instance);

            var ex = Assert.Throws<ScriptRuntimeException>(() =>
                converter.Convert(
                    projectFiles: [new(@"Project.vcxproj")],
                    solutionFile: null,
                    qtVersion: null,
                    enableStandaloneProjectBuilds: false,
                    indentStyle: "spaces",
                    indentSize: 4,
                    dryRun: false));

            Assert.Contains("Unsupported C language standard", ex.Message);
        }        
    }

    public class PreprocessorDefinitionsTests
    {
        static string CreateProjectWithDefines() => $"""
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
                    <ConfigurationType>Application</ConfigurationType>
                    <CharacterSet>NotSet</CharacterSet>
                </PropertyGroup>
                <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Release|Win32'" Label="Configuration">
                    <ConfigurationType>Application</ConfigurationType>
                    <CharacterSet>NotSet</CharacterSet>
                </PropertyGroup>
                <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Debug|Win32'">
                    <ClCompile>
                        <PreprocessorDefinitions>FOO;DEBUG;FOO;VALUE=1;%(PreprocessorDefinitions)</PreprocessorDefinitions>
                    </ClCompile>
                </ItemDefinitionGroup>
                <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Release|Win32'">
                    <ClCompile>
                        <PreprocessorDefinitions>FOO;NDEBUG;VALUE=2;%(PreprocessorDefinitions)</PreprocessorDefinitions>
                    </ClCompile>
                </ItemDefinitionGroup>
            </Project>
            """;

        static string CreateProjectWithMBCS() => $"""
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
                    <ConfigurationType>Application</ConfigurationType>
                    <CharacterSet>MultiByte</CharacterSet>
                </PropertyGroup>
                <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Release|Win32'" Label="Configuration">
                    <ConfigurationType>Application</ConfigurationType>
                    <CharacterSet>MultiByte</CharacterSet>
                </PropertyGroup>
                <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Debug|Win32'">
                    <ClCompile>
                        <PreprocessorDefinitions>DEBUG_DEF;%(PreprocessorDefinitions)</PreprocessorDefinitions>
                    </ClCompile>
                </ItemDefinitionGroup>
                <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Release|Win32'">
                    <ClCompile>
                        <PreprocessorDefinitions>RELEASE_DEF;%(PreprocessorDefinitions)</PreprocessorDefinitions>
                    </ClCompile>
                </ItemDefinitionGroup>
            </Project>
            """;

        static string CreateProjectWithInvalidCharSet() => $"""
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
                    <ConfigurationType>Application</ConfigurationType>
                    <CharacterSet>InvalidCharSet</CharacterSet>
                </PropertyGroup>
                <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Release|Win32'" Label="Configuration">
                    <ConfigurationType>Application</ConfigurationType>
                    <CharacterSet>InvalidCharSet</CharacterSet>
                </PropertyGroup>
            </Project>
            """;

        static string CreateProjectWithArchDefines() => $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Project DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
                <ItemGroup Label="ProjectConfigurations">
                    <ProjectConfiguration Include="Debug|Win32">
                        <Configuration>Debug</Configuration>
                        <Platform>Win32</Platform>
                    </ProjectConfiguration>
                    <ProjectConfiguration Include="Debug|x64">
                        <Configuration>Debug</Configuration>
                        <Platform>x64</Platform>
                    </ProjectConfiguration>
                </ItemGroup>
                <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Debug|Win32'" Label="Configuration">
                    <ConfigurationType>Application</ConfigurationType>
                </PropertyGroup>
                <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Debug|x64'" Label="Configuration">
                    <ConfigurationType>Application</ConfigurationType>
                </PropertyGroup>
                <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Debug|Win32'">
                    <ClCompile>
                        <PreprocessorDefinitions>X86_DEF;%(PreprocessorDefinitions)</PreprocessorDefinitions>
                    </ClCompile>
                </ItemDefinitionGroup>
                <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Debug|x64'">
                    <ClCompile>
                        <PreprocessorDefinitions>X64_DEF;%(PreprocessorDefinitions)</PreprocessorDefinitions>
                    </ClCompile>
                </ItemDefinitionGroup>
            </Project>
            """;

        [Fact]
        public void Given_ProjectWithConfigurationSpecificDefines_When_Converted_Then_GeneratorExpressionsUsedAndDuplicatesRemoved()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProjectWithDefines()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")],
                solutionFile: null,
                qtVersion: null,
                enableStandaloneProjectBuilds: false,
                indentStyle: "spaces",
                indentSize: 4,
                dryRun: false);

            AssertEx.FileHasContent(@"CMakeLists.txt", fileSystem, """
                cmake_minimum_required(VERSION 3.13)
                project(Project)


                add_executable(Project
                )

                target_compile_definitions(Project
                    PUBLIC
                        FOO
                        $<$<CONFIG:Debug>:DEBUG>
                        $<$<CONFIG:Debug>:VALUE=1>
                        $<$<CONFIG:Release>:NDEBUG>
                        $<$<CONFIG:Release>:VALUE=2>
                )
                """);
        }

        [Fact]
        public void Given_ProjectWithMultiByteCharacterSet_When_Converted_Then_MBCSDefinitionAdded()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"ProjectMBCS.vcxproj", new(CreateProjectWithMBCS()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"ProjectMBCS.vcxproj")],
                solutionFile: null,
                qtVersion: null,
                enableStandaloneProjectBuilds: false,
                indentStyle: "spaces",
                indentSize: 4,
                dryRun: false);

            AssertEx.FileHasContent(@"CMakeLists.txt", fileSystem, """
                cmake_minimum_required(VERSION 3.13)
                project(ProjectMBCS)


                add_executable(ProjectMBCS
                )

                target_compile_definitions(ProjectMBCS
                    PUBLIC
                        _MBCS
                        $<$<CONFIG:Debug>:DEBUG_DEF>
                        $<$<CONFIG:Release>:RELEASE_DEF>
                )
                """);
        }

        [Fact]
        public void Given_ProjectWithInvalidCharacterSet_When_Converted_Then_Throws()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProjectWithInvalidCharSet()));

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

            Assert.Contains("Invalid value for CharacterSet", ex.Message);
        }

        [Fact]
        public void Given_ProjectWithArchitectureSpecificDefines_When_Converted_Then_UsesGeneratorExpressionsForArchitecture()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"ProjectArch.vcxproj", new(CreateProjectWithArchDefines()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"ProjectArch.vcxproj")],
                solutionFile: null,
                qtVersion: null,
                enableStandaloneProjectBuilds: false,
                indentStyle: "spaces",
                indentSize: 4,
                dryRun: false);

            AssertEx.FileHasContent(@"CMakeLists.txt", fileSystem, """
                cmake_minimum_required(VERSION 3.13)
                project(ProjectArch)


                add_executable(ProjectArch
                )

                target_compile_definitions(ProjectArch
                    PUBLIC
                        $<$<STREQUAL:$<CMAKE_CXX_COMPILER_ARCHITECTURE_ID>,X86>:X86_DEF>
                        $<$<STREQUAL:$<CMAKE_CXX_COMPILER_ARCHITECTURE_ID>,x64>:X64_DEF>
                )
                """);
        }
    }

    public class LinkerLibraryDirectoriesTests
    {
        static string CreateProjectWithLibraryDirs(string debugDirs, string releaseDirs) => $"""
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
                        <AdditionalLibraryDirectories>{debugDirs}</AdditionalLibraryDirectories>
                    </Link>
                </ItemDefinitionGroup>
                <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Release|Win32'">
                    <Link>
                        <AdditionalLibraryDirectories>{releaseDirs}</AdditionalLibraryDirectories>
                    </Link>
                </ItemDefinitionGroup>
            </Project>
            """;

        [Fact]
        public void Given_LinkerPathsSameForAllConfigs_When_Converted_Then_TargetLinkDirectoriesAdded()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProjectWithLibraryDirs("C:\\Lib\\", "C:\\Lib\\")));

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
            Assert.Contains("""
                target_link_directories(Project
                    PUBLIC
                        C:/Lib
                )
                """.Trim(), cmake);
        }

        [Fact]
        public void Given_LinkerPathsDifferentPerConfig_When_Converted_Then_GeneratorExpressionsUsed()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProjectWithLibraryDirs("DebugLibs", "ReleaseLibs")));

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
            Assert.Contains("""
                target_link_directories(Project
                    PUBLIC
                        $<$<CONFIG:Debug>:DebugLibs>
                        $<$<CONFIG:Release>:ReleaseLibs>
                )
                """.Trim(), cmake);
        }

        [Fact]
        public void Given_LinkerPathsWithMSBuildMacros_When_Converted_Then_MacrosAreTranslated()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProjectWithLibraryDirs("$(ProjectDir)libs;$(Configuration)", "$(ProjectDir)libs;$(Configuration)")));

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
            Assert.Contains("""
                target_link_directories(Project
                    PUBLIC
                        ${CMAKE_CURRENT_SOURCE_DIR}/libs
                        ${CMAKE_BUILD_TYPE}
                )
                """.Trim(), cmake);
        }
    }
}
