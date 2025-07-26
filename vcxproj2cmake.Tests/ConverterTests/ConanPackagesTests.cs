using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
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
                projectFiles: [new(@"Project.vcxproj")]);

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
                projectFiles: [new(@"Project.vcxproj")]);

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
                projectFiles: [new(@"Project.vcxproj")]);

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
}
