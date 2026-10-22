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
                cmake_minimum_required(VERSION 4.0)
                project(Dll)

                add_library(Dll SHARED)
                """);

            Assert.FileHasContent(Path.Combine("App", "CMakeLists.txt"), fileSystem, """
                cmake_minimum_required(VERSION 4.0)
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
                cmake_minimum_required(VERSION 4.0)
                project(Dll)

                add_library(Dll STATIC)
                """);

            Assert.FileHasContent(Path.Combine("App", "CMakeLists.txt"), fileSystem, """
                cmake_minimum_required(VERSION 4.0)
                project(App)

                add_executable(App)

                target_link_libraries(App
                    PRIVATE
                        Dll
                )
                """);
        }

        [Fact]
        public void Given_StaticLibraryReferenceChain_When_Converted_Then_OnlyDirectReferencesAreLinkedAndPropagated()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(Path.Combine("Lib1", "Lib1.vcxproj"), new(TestData.Project()
                .WithProperty("ConfigurationType", "StaticLibrary")
                .Build()));
            fileSystem.AddFile(Path.Combine("Lib2", "Lib2.vcxproj"), new(TestData.Project()
                .WithProperty("ConfigurationType", "StaticLibrary")
                .WithProjectReferences("..\\Lib1\\Lib1.vcxproj")
                .Build()));
            fileSystem.AddFile(Path.Combine("App", "App.vcxproj"), new(TestData.Project()
                .WithProperty("ConfigurationType", "Application")
                .WithProjectReferences("..\\Lib2\\Lib2.vcxproj")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(projectFiles:
            [
                new(Path.Combine("App", "App.vcxproj")),
                new(Path.Combine("Lib2", "Lib2.vcxproj")),
                new(Path.Combine("Lib1", "Lib1.vcxproj"))
            ]);

            // Assert
            Assert.FileHasContent(Path.Combine("Lib2", "CMakeLists.txt"), fileSystem, """
                cmake_minimum_required(VERSION 4.0)
                project(Lib2)

                add_library(Lib2 STATIC)

                target_link_libraries(Lib2
                    PUBLIC
                        Lib1
                )
                """);

            Assert.FileHasContent(Path.Combine("App", "CMakeLists.txt"), fileSystem, """
                cmake_minimum_required(VERSION 4.0)
                project(App)

                add_executable(App)

                target_link_libraries(App
                    PRIVATE
                        Lib2
                )
                """);
        }

        [Fact]
        public void Given_ProjectReferencesHeaderOnlyLibrary_When_Converted_Then_LibraryIsLinked()
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
            Assert.FileHasContent(Path.Combine("App", "CMakeLists.txt"), fileSystem, """
                cmake_minimum_required(VERSION 4.0)
                project(App)

                add_executable(App)

                target_link_libraries(App
                    PRIVATE
                        HeaderOnly
                )
                """);
        }

        [Fact]
        public void Given_HeaderOnlyLibraryReferencesLibrary_When_Converted_Then_ReferenceIsAnInterfaceDependency()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(Path.Combine("Lib", "Lib.vcxproj"), new(TestData.Project()
                .WithProperty("ConfigurationType", "StaticLibrary")
                .Build()));
            fileSystem.AddFile(Path.Combine("HeaderOnly", "HeaderOnly.vcxproj"), new(TestData.Project()
                .WithProperty("ConfigurationType", "StaticLibrary")
                .WithItems("ClInclude", "header.hpp")
                .WithProjectReferences("..\\Lib\\Lib.vcxproj")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(Path.Combine("HeaderOnly", "HeaderOnly.vcxproj")), new(Path.Combine("Lib", "Lib.vcxproj"))]);

            // Assert
            Assert.FileHasContent(Path.Combine("HeaderOnly", "CMakeLists.txt"), fileSystem, """
                cmake_minimum_required(VERSION 4.0)
                project(HeaderOnly)

                add_library(HeaderOnly INTERFACE)

                target_link_libraries(HeaderOnly
                    INTERFACE
                        Lib
                )
                """);
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
