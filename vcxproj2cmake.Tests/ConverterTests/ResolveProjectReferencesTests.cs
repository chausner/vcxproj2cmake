using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class ResolveProjectReferencesTests
    {
        [Fact]
        public void Given_ProjectReferencesUnknownProject_When_Converted_Then_Throws()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(Path.Combine("App", "App.vcxproj"), new(TestData.CreateProject("Application", "..\\Missing\\Missing.vcxproj")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act & Assert
            var ex = Assert.Throws<CatastrophicFailureException>(() =>
                converter.Convert(
                    projectFiles: [new(Path.Combine("App", "App.vcxproj"))]));
            Assert.Matches(@"Project .+ references project .+ which is not part of the solution or the list of projects\.", ex.Message);
        }

        [Fact]
        public void Given_ProjectReferencesAnotherProjectWithRelativePath_When_Converted_Then_MatchesExpectedOutput()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(Path.Combine("Lib", "Lib.vcxproj"), new(TestData.CreateProject("StaticLibrary"))); 
            fileSystem.AddFile(Path.Combine("App", "App.vcxproj"), new(TestData.CreateProject("Application", "..\\Lib\\Lib.vcxproj")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(Path.Combine("App", "App.vcxproj")), new(Path.Combine("Lib", "Lib.vcxproj"))]);

            // Assert
            AssertEx.FileHasContent(Path.Combine("Lib", "CMakeLists.txt"), fileSystem, """
                cmake_minimum_required(VERSION 3.24)
                project(Lib)


                add_library(Lib STATIC)
                """);

            AssertEx.FileHasContent(Path.Combine("App", "CMakeLists.txt"), fileSystem, """
                cmake_minimum_required(VERSION 3.24)
                project(App)


                add_executable(App)

                target_link_libraries(App
                    PUBLIC
                        Lib
                )
                """);
        }

        [Fact]
        public void Given_ProjectReferencesAnotherProjectWithAbsolutePath_When_Converted_Then_MatchesExpectedOutput()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            var absoluteLibPath = Path.GetFullPath(Path.Combine("Lib", "Lib.vcxproj"));
            fileSystem.AddFile(Path.Combine("Lib", "Lib.vcxproj"), new(TestData.CreateProject("StaticLibrary")));
            fileSystem.AddFile(Path.Combine("App", "App.vcxproj"), new(TestData.CreateProject("Application", absoluteLibPath)));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(Path.Combine("App", "App.vcxproj")), new(Path.Combine("Lib", "Lib.vcxproj"))]);

            // Assert
            AssertEx.FileHasContent(Path.Combine("Lib", "CMakeLists.txt"), fileSystem, """
                cmake_minimum_required(VERSION 3.24)
                project(Lib)


                add_library(Lib STATIC)
                """);

            AssertEx.FileHasContent(Path.Combine("App", "CMakeLists.txt"), fileSystem, """
                cmake_minimum_required(VERSION 3.24)
                project(App)


                add_executable(App)

                target_link_libraries(App
                    PUBLIC
                        Lib
                )
                """);
        }
    }
}
