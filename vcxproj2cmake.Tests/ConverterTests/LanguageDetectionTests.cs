using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
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
            <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Debug|Win32'" Label="Configuration">
                <UseDebugLibraries>true</UseDebugLibraries>
            </PropertyGroup>
            <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Release|Win32'" Label="Configuration">
                <UseDebugLibraries>false</UseDebugLibraries>
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
                projectFiles: [new(@"Project.vcxproj")]);

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
                projectFiles: [new(@"Project.vcxproj")]);

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
                projectFiles: [new(@"Project.vcxproj")]);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("project(Project LANGUAGES CXX)", cmake);
        }

        [Fact]
        public void Given_ProjectWithCAndCppFiles_When_Converted_Then_LanguagesAreCAndCxx()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProject("main.c", "main.cpp")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("project(Project LANGUAGES C CXX)", cmake);
        }
    }
}
