using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class TranslateMSBuildMacrosTests
    {
        [Fact]
        public void Given_ProjectPropertiesWithMSBuildMacros_When_Converted_Then_MacrosAreReplacedByCMakeEquivalents()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithItems("ClCompile",
                    "$(ConFIGuration).cpp",
                    "$(ConFIGurationName).cpp",
                    "$(ProJECtDir)SomeFile.cpp",
                    "$(ProJECtName).cpp",
                    "$(SolUTIonDir)SomeFile.cpp",
                    "$(SolUTIonName).cpp")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("""
                target_sources(Project
                    PRIVATE
                        "${CMAKE_BUILD_TYPE}.cpp"
                        "${CMAKE_BUILD_TYPE}.cpp"
                        "${CMAKE_CURRENT_SOURCE_DIR}/SomeFile.cpp"
                        "${CMAKE_PROJECT_NAME}.cpp"
                        "${CMAKE_SOURCE_DIR}/SomeFile.cpp"
                        "${PROJECT_NAME}.cpp"
                )
                """, cmake);
        }

        [Fact]
        public void Given_UnsupportedMSBuildMacrosInPreprocessorDefinitions_When_Converted_Then_WarnsAndTranslatesMacros()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithItemDefinitionSetting("ClCompile", "PreprocessorDefinitions", "NAME=$(Foo)_$(Bar)")
                .Build()));

            var logger = new InMemoryLogger();
            var converter = new Converter(fileSystem, logger);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("\"NAME=${Foo}_${Bar}\"", cmake);
            Assert.Contains(
                "Setting PreprocessorDefinitions with value \"NAME=\\$(Foo)_\\$(Bar)\" contains unsupported MSBuild macros/properties: Foo, Bar",
                logger.AllMessageText);
        }
    }
}
