using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class EnableStandaloneProjectBuildsTests
    {
        [Fact]
        public void Given_ProjectReferencesAnotherProject_When_ConvertedWithEnableStandaloneProjectBuilds_Then_ProjectFileContainsAddSubdirectory()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Lib/Lib.vcxproj", new(TestData.CreateProject("StaticLibrary")));
            fileSystem.AddFile(@"App/App.vcxproj", new(TestData.CreateProject("Application", "..\\Lib\\Lib.vcxproj")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"App/App.vcxproj"), new(@"Lib/Lib.vcxproj")],
                enableStandaloneProjectBuilds: true);

            // Assert
            AssertEx.FileHasContent(@"App/CMakeLists.txt", fileSystem, """
                cmake_minimum_required(VERSION 3.24)
                project(App)

                if(NOT TARGET Lib)
                    add_subdirectory(../Lib "${CMAKE_BINARY_DIR}/Lib")
                endif()


                add_executable(App)

                target_link_libraries(App
                    PUBLIC
                        Lib
                )
                """);
        }
    }
}
