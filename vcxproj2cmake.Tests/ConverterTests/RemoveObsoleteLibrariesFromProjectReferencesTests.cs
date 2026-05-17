using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class RemoveObsoleteLibrariesFromProjectReferencesTests
    {
        [Fact]
        public void Given_ProjectLinksLibraryExplicitlyAndLinkLibraryDependenciesDisabled_When_Converted_Then_LibraryIsPreserved()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Lib/Lib.vcxproj", new(TestData.Project()
                .WithProperty("ConfigurationType", "StaticLibrary")
                .Build()));
            fileSystem.AddFile(@"App/App.vcxproj", new(TestData.Project()
                .WithProjectReferences(@"..\Lib\Lib.vcxproj")
                .WithItemDefinitionSetting("ProjectReference", "LinkLibraryDependencies", "false")
                .WithItemDefinitionSetting("Link", "AdditionalDependencies", "Lib.lib;%(AdditionalDependencies)")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"App/App.vcxproj"), new(@"Lib/Lib.vcxproj")]);

            // Assert
            Assert.FileHasContent(@"App/CMakeLists.txt", fileSystem, """
                cmake_minimum_required(VERSION 3.24)
                project(App)

                add_executable(App)

                target_link_libraries(App
                    PRIVATE
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

            fileSystem.AddFile(@"Lib/Lib.vcxproj", new(TestData.Project()
                .WithProperty("ConfigurationType", "StaticLibrary")
                .Build()));
            fileSystem.AddFile(@"App/App.vcxproj", new(TestData.Project()
                .WithProjectReferences(@"..\Lib\Lib.vcxproj")
                .WithItemDefinitionSetting("ProjectReference", "LinkLibraryDependencies", "true")
                .WithItemDefinitionSetting("Link", "AdditionalDependencies", "Lib.lib;%(AdditionalDependencies)")
                .Build()));

            var logger = new InMemoryLogger();
            var converter = new Converter(fileSystem, logger);

            // Act
            converter.Convert(
                projectFiles: [new(@"App/App.vcxproj"), new(@"Lib/Lib.vcxproj")]);

            // Assert
            Assert.FileHasContent(@"App/CMakeLists.txt", fileSystem, """
                cmake_minimum_required(VERSION 3.24)
                project(App)

                add_executable(App)

                target_link_libraries(App
                    PRIVATE
                        Lib
                )
                """);

            Assert.Contains(
                "Removing explicit library dependency Lib.lib from project App since LinkLibraryDependencies is enabled.",
                logger.AllMessageText);
        }

        [Fact]
        public void Given_ProjectLinksLibraryExplicitlyAndLinkLibraryDependenciesEnabledAndProjectTargetNameIsOverridden_When_Converted_Then_LibraryIsRemovedAndLogged()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Lib/Lib.vcxproj", new(TestData.Project()
                .WithProperty("ConfigurationType", "StaticLibrary")
                .WithProperty("TargetName", "MyLib")
                .Build()));
            fileSystem.AddFile(@"App/App.vcxproj", new(TestData.Project()
                .WithProjectReferences(@"..\Lib\Lib.vcxproj")
                .WithItemDefinitionSetting("ProjectReference", "LinkLibraryDependencies", "true")
                .WithItemDefinitionSetting("Link", "AdditionalDependencies", "MyLib.lib;%(AdditionalDependencies)")
                .Build()));

            var logger = new InMemoryLogger();
            var converter = new Converter(fileSystem, logger);

            // Act
            converter.Convert(
                projectFiles: [new(@"App/App.vcxproj"), new(@"Lib/Lib.vcxproj")]);

            // Assert
            Assert.FileHasContent(@"App/CMakeLists.txt", fileSystem, """
                cmake_minimum_required(VERSION 3.24)
                project(App)

                add_executable(App)

                target_link_libraries(App
                    PRIVATE
                        Lib
                )
                """);

            Assert.Contains(
                "Removing explicit library dependency MyLib.lib from project App since LinkLibraryDependencies is enabled.",
                logger.AllMessageText);
        }
    }
}
