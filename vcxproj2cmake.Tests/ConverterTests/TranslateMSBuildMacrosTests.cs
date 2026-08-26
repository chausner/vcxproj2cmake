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
                    "$(MSBuildThisFileDirectory)SomeFile.cpp",
                    "$(MSBuildProjectDirectory)\\ProjectDirectory.cpp",
                    "$(ProJECtDir)SomeFile.cpp",
                    "$(ProJECtName).cpp",
                    "$(SolUTIonDir)SomeFile.cpp",
                    "$(SolUTIonName).cpp")
                .WithClCompileSetting(
                    "PreprocessorDefinitions",
                    "PROJECT_DIR=$(MSBuildProjectDirectory);PROJECT_NAME=$(MSBuildProjectName);THIS_FILE_DIR=$(MSBuildThisFileDirectory);THIS_FILE_NAME=$(MSBuildThisFileName);OUT_DIR=$(OutDir);TARGET_DIR=$(TargetDir);TARGET_EXT=$(TargetExt);TARGET_FILE_NAME=$(TargetFileName);TARGET_NAME=$(TargetName);TARGET_PATH=$(TargetPath)")
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
                        "${CMAKE_CURRENT_SOURCE_DIR}/ProjectDirectory.cpp"
                        "${CMAKE_CURRENT_SOURCE_DIR}/SomeFile.cpp"
                        "${CMAKE_CURRENT_SOURCE_DIR}/SomeFile.cpp"
                        "${CMAKE_PROJECT_NAME}.cpp"
                        "${CMAKE_SOURCE_DIR}/SomeFile.cpp"
                        "${PROJECT_NAME}.cpp"
                        "$<CONFIG>.cpp"
                        "$<CONFIG>.cpp"
                )
                """, cmake);
            Assert.Contains("""
                target_compile_definitions(Project
                    PRIVATE
                        "PROJECT_DIR=${CMAKE_CURRENT_SOURCE_DIR}"
                        "PROJECT_NAME=${PROJECT_NAME}"
                        "THIS_FILE_DIR=${CMAKE_CURRENT_SOURCE_DIR}/"
                        "THIS_FILE_NAME=${PROJECT_NAME}"
                        "OUT_DIR=$<TARGET_FILE_DIR:Project>/"
                        "TARGET_DIR=$<TARGET_FILE_DIR:Project>/"
                        "TARGET_EXT=$<TARGET_FILE_SUFFIX:Project>"
                        "TARGET_FILE_NAME=$<TARGET_FILE_NAME:Project>"
                        "TARGET_NAME=$<TARGET_FILE_BASE_NAME:Project>"
                        "TARGET_PATH=$<TARGET_FILE:Project>"
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
