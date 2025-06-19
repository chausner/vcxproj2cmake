using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Scriban.Syntax;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
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
                projectFiles: [new(@"App.vcxproj")]);

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
                projectFiles: [new(@"Lib.vcxproj")]);

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
                projectFiles: [new(@"Dll.vcxproj")]);

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
                projectFiles: [new(@"HeaderOnly.vcxproj")]);

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
                    projectFiles: [new(@"Bad.vcxproj")]));

            Assert.Contains("Unsupported configuration type", ex.Message);
        }
    }
}
