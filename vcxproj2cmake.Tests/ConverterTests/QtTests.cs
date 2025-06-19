using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
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
                    projectFiles: [new(@"QtProject.vcxproj")]));

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
                    qtVersion: 6));

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
                qtVersion: 6);

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
}
