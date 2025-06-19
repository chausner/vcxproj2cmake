using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
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
                projectFiles: [new(Path.Combine("App", "App.vcxproj")), new(Path.Combine("Dll", "Dll.vcxproj"))]);

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
                projectFiles: [new(Path.Combine("App", "App.vcxproj")), new(Path.Combine("HeaderOnly", "HeaderOnly.vcxproj"))]);

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
                projectFiles: [new(Path.Combine("App", "App.vcxproj")), new(Path.Combine("Exe", "Exe.vcxproj"))]);

            var cmake = fileSystem.GetFile(Path.Combine("App", "CMakeLists.txt")).TextContents;
            Assert.DoesNotContain("target_link_libraries(App", cmake);
        }
    }
}
