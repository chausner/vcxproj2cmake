using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
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
                projectFiles: [new(@"App/App.vcxproj"), new(@"Lib/Lib.vcxproj")]);

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
                projectFiles: [new(@"App/App.vcxproj"), new(@"Lib/Lib.vcxproj")]);

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
}
