using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class QtTests
    {
        [Fact]
        public void Given_QtProjectWithoutQtVersion_When_Converted_Then_Throws()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"QtProject.vcxproj", new(TestData.Project()
                .WithProperty("QtModules", "core")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act & Assert
            var ex = Assert.Throws<CatastrophicFailureException>(() =>
                converter.Convert(
                    projectFiles: [new(@"QtProject.vcxproj")]));

            Assert.Equal("Project uses Qt but no Qt version is set. Specify the version with --qt-version.", ex.Message);
        }

        [Fact]
        public void Given_QtProjectWithUnknownModule_When_Converted_Then_Throws()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"QtProject.vcxproj", new(TestData.Project()
                .WithProperty("QtModules", "doesnotexist")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act & Assert
            var ex = Assert.Throws<CatastrophicFailureException>(() =>
                converter.Convert(
                    projectFiles: [new(@"QtProject.vcxproj")],
                    qtVersion: 6));

            Assert.Contains("Unknown Qt module", ex.Message);
        }

        [Fact]
        public void Given_QtProjectWithQtVersionAndModules_When_Converted_Then_MatchesExpectedOutput()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"QtProject.vcxproj", new(TestData.Project()
                .WithProperty("QtModules", "core;widgets")
                .WithItems("QtMoc", "moc.h")
                .WithItems("QtUic", "form.ui")
                .WithItems("QtRcc", "res.qrc")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"QtProject.vcxproj")],
                qtVersion: 6);

            // Assert
            Assert.FileHasContent(@"CMakeLists.txt", fileSystem, """
                cmake_minimum_required(VERSION 4.0)
                project(QtProject)

                find_package(Qt6 REQUIRED COMPONENTS Core Widgets)

                add_executable(QtProject)

                target_sources(QtProject
                    PRIVATE
                        form.ui
                        res.qrc
                )

                set_target_properties(QtProject PROPERTIES
                    AUTOMOC ON
                    AUTOUIC ON
                    AUTORCC ON
                )

                target_link_libraries(QtProject
                    PRIVATE
                        Qt6::Core
                        Qt6::Widgets
                )
                """);
        }
    }
}
