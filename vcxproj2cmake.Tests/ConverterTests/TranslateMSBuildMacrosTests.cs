using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class TranslateMSBuildMacrosTests
    {
        static string CreateProjectWithUnsupportedMacroDefinitions() => $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Project DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
                <ItemGroup Label="ProjectConfigurations">
                    <ProjectConfiguration Include="Release|Win32">
                        <Configuration>Release</Configuration>
                        <Platform>Win32</Platform>
                    </ProjectConfiguration>
                </ItemGroup>
                <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Release|Win32'">
                    <ClCompile>
                        <PreprocessorDefinitions>NAME=$(Foo)_$(Bar)</PreprocessorDefinitions>
                    </ClCompile>
                </ItemDefinitionGroup>
            </Project>
            """;

        [Fact]
        public void Given_ProjectPropertiesWithMSBuildMacros_When_Converted_Then_MacrosAreReplacedByCMakeEquivalents()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.CreateProjectWithSources(
                "$(ConFIGuration).cpp",
                "$(ConFIGurationName).cpp",
                "$(ProJECtDir)SomeFile.cpp",
                "$(ProJECtName).cpp",
                "$(SolUTIonDir)SomeFile.cpp",
                "$(SolUTIonName).cpp")));

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

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProjectWithUnsupportedMacroDefinitions()));

            var logger = new InMemoryLogger();
            var converter = new Converter(fileSystem, logger);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("NAME=${Foo}_${Bar}", cmake);
            Assert.Contains(
                "Setting PreprocessorDefinitions with value \"NAME=$(Foo)_$(Bar)\" contains unsupported MSBuild macros/properties: Foo, Bar",
                logger.AllMessageText);
        }
    }
}
