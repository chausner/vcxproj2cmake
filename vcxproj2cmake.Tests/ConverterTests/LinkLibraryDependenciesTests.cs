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
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(Path.Combine("Dll", "Dll.vcxproj"), new(TestData.Project()
                .WithProperty("ConfigurationType", "DynamicLibrary")
                .Build()));
            fileSystem.AddFile(Path.Combine("App", "App.vcxproj"), new(TestData.Project()
                .WithProperty("ConfigurationType", "Application")
                .WithProjectReferences("..\\Dll\\Dll.vcxproj")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(Path.Combine("App", "App.vcxproj")), new(Path.Combine("Dll", "Dll.vcxproj"))]);

            // Assert
            Assert.FileHasContent(Path.Combine("Dll", "CMakeLists.txt"), fileSystem, """
                cmake_minimum_required(VERSION 3.24)
                project(Dll)

                add_library(Dll SHARED)
                """);

            Assert.FileHasContent(Path.Combine("App", "CMakeLists.txt"), fileSystem, """
                cmake_minimum_required(VERSION 3.24)
                project(App)

                add_executable(App)

                target_link_libraries(App
                    PRIVATE
                        Dll
                )
                """);
        }

        [Fact]
        public void Given_ProjectReferencesStaticLibrary_When_Converted_Then_LibraryIsLinked()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(Path.Combine("Dll", "Dll.vcxproj"), new(TestData.Project()
                .WithProperty("ConfigurationType", "StaticLibrary")
                .Build()));
            fileSystem.AddFile(Path.Combine("App", "App.vcxproj"), new(TestData.Project()
                .WithProperty("ConfigurationType", "Application")
                .WithProjectReferences("..\\Dll\\Dll.vcxproj")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(Path.Combine("App", "App.vcxproj")), new(Path.Combine("Dll", "Dll.vcxproj"))]);

            // Assert
            Assert.FileHasContent(Path.Combine("Dll", "CMakeLists.txt"), fileSystem, """
                cmake_minimum_required(VERSION 3.24)
                project(Dll)

                add_library(Dll STATIC)
                """);

            Assert.FileHasContent(Path.Combine("App", "CMakeLists.txt"), fileSystem, """
                cmake_minimum_required(VERSION 3.24)
                project(App)

                add_executable(App)

                target_link_libraries(App
                    PRIVATE
                        Dll
                )
                """);
        }

        [Fact]
        public void Given_ProjectReferencesHeaderOnlyLibrary_When_Converted_Then_NoLibraryIsLinked()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(Path.Combine("HeaderOnly", "HeaderOnly.vcxproj"), new(TestData.Project()
                .WithProperty("ConfigurationType", "StaticLibrary")
                .WithItems("ClInclude", "header.hpp")
                .Build()));
            fileSystem.AddFile(Path.Combine("App", "App.vcxproj"), new(TestData.Project()
                .WithProperty("ConfigurationType", "Application")
                .WithProjectReferences("..\\HeaderOnly\\HeaderOnly.vcxproj")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(Path.Combine("App", "App.vcxproj")), new(Path.Combine("HeaderOnly", "HeaderOnly.vcxproj"))]);

            // Assert
            var cmake = fileSystem.GetFile(Path.Combine("App", "CMakeLists.txt")).TextContents;

            Assert.DoesNotContain("target_link_libraries(App", cmake);
        }

        [Fact]
        public void Given_ProjectReferencesApplication_When_Converted_Then_NoLibraryIsLinked()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(Path.Combine("Exe", "Exe.vcxproj"), new(TestData.Project()
                .WithProperty("ConfigurationType", "Application")
                .Build()));
            fileSystem.AddFile(Path.Combine("App", "App.vcxproj"), new(TestData.Project()
                .WithProperty("ConfigurationType", "Application")
                .WithProjectReferences("..\\Exe\\Exe.vcxproj")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(Path.Combine("App", "App.vcxproj")), new(Path.Combine("Exe", "Exe.vcxproj"))]);

            // Assert
            var cmake = fileSystem.GetFile(Path.Combine("App", "CMakeLists.txt")).TextContents;

            Assert.DoesNotContain("target_link_libraries(App", cmake);
        }
    }
}
