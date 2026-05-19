using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class ConfigurationTypeTests
    {
        [Fact]
        public void Given_ApplicationProject_When_Converted_Then_UsesAddExecutable()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"App.vcxproj", new(TestData.Project()
                .WithProperty("ConfigurationType", "Application")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"App.vcxproj")]);

            // Assert
            Assert.FileHasContent(@"CMakeLists.txt", fileSystem, """
                cmake_minimum_required(VERSION 4.0)
                project(App)

                add_executable(App)

                """);
        }

        [Fact]
        public void Given_StaticLibraryProject_When_Converted_Then_UsesAddLibraryStatic()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Lib.vcxproj", new(TestData.Project()
                .WithProperty("ConfigurationType", "StaticLibrary")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Lib.vcxproj")]);

            // Assert
            Assert.FileHasContent(@"CMakeLists.txt", fileSystem, """
                cmake_minimum_required(VERSION 4.0)
                project(Lib)

                add_library(Lib STATIC)
                """);
        }

        [Fact]
        public void Given_DynamicLibraryProject_When_Converted_Then_UsesAddLibraryShared()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Dll.vcxproj", new(TestData.Project()
                .WithProperty("ConfigurationType", "DynamicLibrary")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Dll.vcxproj")]);

            // Assert
            Assert.FileHasContent(@"CMakeLists.txt", fileSystem, """
                cmake_minimum_required(VERSION 4.0)
                project(Dll)

                add_library(Dll SHARED)
                """);
        }

        [Fact]
        public void Given_HeaderOnlyLibrary_When_Converted_Then_UsesAddLibraryInterface()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"HeaderOnly.vcxproj", new(TestData.Project()
                .WithProperty("ConfigurationType", "StaticLibrary")
                .WithItems("ClInclude", "header.hpp")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"HeaderOnly.vcxproj")]);

            // Assert
            Assert.FileHasContent(@"CMakeLists.txt", fileSystem, """
                cmake_minimum_required(VERSION 4.0)
                project(HeaderOnly)

                add_library(HeaderOnly INTERFACE)
                """);
        }

        [Fact]
        public void Given_ProjectWithUnsupportedConfigurationType_When_Converted_Then_Throws()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Bad.vcxproj", new(TestData.Project()
                .WithProperty("ConfigurationType", "Makefile")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act & Assert
            var ex = Assert.Throws<CatastrophicFailureException>(() =>
                converter.Convert(
                    projectFiles: [new(@"Bad.vcxproj")]));

            Assert.Contains("ConfigurationType property is unsupported: Makefile", ex.Message);
        }
    }
}
