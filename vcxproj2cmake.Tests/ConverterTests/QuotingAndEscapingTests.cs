using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class QuotingAndEscapingTests
    {
        [Fact]
        public void Given_EmptyProjectWithSpecialCharactersInName_When_Converted_Then_ProjectNameIsQuotedAndEscaped()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project with $pecial c#aracters.vcxproj", new(TestData.EmptyProject));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project with $pecial c#aracters.vcxproj")]);

            // Assert
            AssertEx.FileHasContent(@"CMakeLists.txt", fileSystem, """            
                cmake_minimum_required(VERSION 3.13)
                project("Project with $pecial c#aracters")


                add_executable("Project with $pecial c#aracters"
                )

                target_compile_definitions("Project with $pecial c#aracters"
                    PUBLIC
                        WIN32
                        _CONSOLE
                        UNICODE
                        _UNICODE
                        $<$<CONFIG:Debug>:_DEBUG>
                        $<$<CONFIG:Release>:NDEBUG>
                )

                target_compile_options("Project with $pecial c#aracters"
                    PUBLIC
                        /W3
                )            
                """);
        }
    }
}
