using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class IndentStyleAndIndentSizeTests
    {

        [Fact]
        public void Given_EmptyProject_When_ConvertedWithIndentStyleTabs_Then_MatchesExpectedOutput()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"EmptyProject.vcxproj", new(TestData.EmptyProject));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"EmptyProject.vcxproj")],
                indentStyle: IndentStyle.Tabs);

            // Assert
            AssertEx.FileHasContent(@"CMakeLists.txt", fileSystem, $"""            
                cmake_minimum_required(VERSION 3.15)
                project(EmptyProject)


                add_executable(EmptyProject
                )

                target_compile_definitions(EmptyProject
                {"\t"}PUBLIC
                {"\t"}{"\t"}WIN32
                {"\t"}{"\t"}_CONSOLE
                {"\t"}{"\t"}UNICODE
                {"\t"}{"\t"}_UNICODE
                {"\t"}{"\t"}$<$<CONFIG:Debug>:_DEBUG>
                {"\t"}{"\t"}$<$<CONFIG:Release>:NDEBUG>
                )

                target_compile_options(EmptyProject
                {"\t"}PUBLIC
                {"\t"}{"\t"}/W3
                )            
                """);
        }

        [Fact]
        public void Given_EmptyProject_When_ConvertedWithIndentSize2_Then_MatchesExpectedOutput()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"EmptyProject.vcxproj", new(TestData.EmptyProject));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"EmptyProject.vcxproj")],
                indentSize: 2);

            // Assert
            AssertEx.FileHasContent(@"CMakeLists.txt", fileSystem, """            
                cmake_minimum_required(VERSION 3.15)
                project(EmptyProject)


                add_executable(EmptyProject
                )

                target_compile_definitions(EmptyProject
                  PUBLIC
                    WIN32
                    _CONSOLE
                    UNICODE
                    _UNICODE
                    $<$<CONFIG:Debug>:_DEBUG>
                    $<$<CONFIG:Release>:NDEBUG>
                )

                target_compile_options(EmptyProject
                  PUBLIC
                    /W3
                )            
                """);
        }
    }
}
