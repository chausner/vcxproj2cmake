using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class EscapedValuesTests
    {
        [Fact]
        public void Given_EscapedScalarAndListValues_When_Converted_Then_UnescapedValuesAreWrittenWithoutSplittingEscapedSeparators()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);
            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithProperty("TargetName", "My%20Target%23%25")
                .WithItemDefinitionSetting("ClCompile", "AdditionalIncludeDirectories", "include%3Bdir;folder%20name;%(AdditionalIncludeDirectories)")
                .WithItemDefinitionSetting("ClCompile", "PreprocessorDefinitions", "VALUE=a%3Bb;SECOND=100%25;%(PreprocessorDefinitions)")
                .WithItemDefinitionSetting("ClCompile", "AdditionalOptions", "/DNAME%3Dfoo%20bar /DVALUE%3D100%25")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains(
                """
                set_target_properties(Project PROPERTIES
                    OUTPUT_NAME "My Target#%"
                """,
                cmake);
            Assert.Contains(
                """
                target_include_directories(Project
                    PRIVATE
                        "${CMAKE_CURRENT_SOURCE_DIR}/include;dir"
                        "${CMAKE_CURRENT_SOURCE_DIR}/folder name"
                )
                """,
                cmake);
            Assert.Contains(
                """
                target_compile_definitions(Project
                    PRIVATE
                        "VALUE=a;b"
                        SECOND=100%
                )
                """,
                cmake);
            Assert.Contains(
                """
                target_compile_options(Project
                    PRIVATE
                        "/DNAME=foo bar"
                        /DVALUE=100%
                )
                """,
                cmake);
        }

        [Fact]
        public void Given_EscapedPathLikeValues_When_Converted_Then_UnescapedPathsImportsAndProjectReferencesAreWritten()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);
            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithImports("packages%2Fconan_my%2Dpkg.props")
                .WithItems("ClCompile", "src%2Fmain.cpp")
                .WithItems("ClInclude", "include%5Cmy%23header.hpp")
                .WithProjectReferences("libs%2FMy%20Lib.vcxproj")
                .Build()));
            fileSystem.AddFile(Path.Combine("libs", "My Lib.vcxproj"), new(TestData.Project()
                .WithProperty("ConfigurationType", "StaticLibrary")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj"), new(Path.Combine("libs", "My Lib.vcxproj"))],
                includeHeaders: true);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains(
                """
                target_sources(Project
                    PRIVATE
                        "include/my#header.hpp"
                        src/main.cpp
                )
                """,
                cmake);
            Assert.Contains("find_package(my-pkg REQUIRED CONFIG)", cmake);
            Assert.Contains(
                """
                target_link_libraries(Project
                    PRIVATE
                        my-pkg::my-pkg
                        "My Lib"
                )
                """,
                cmake);
        }
    }
}
