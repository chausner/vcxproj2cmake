using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public class MSBuildProjectTests
{
    public class ParseProjectFileTests
    {
        [Fact]
        public void Given_ElementWithIgnoredCondition_When_Parsed_Then_WarningIncludesElementNameAndConditionValue()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);
            var logger = new InMemoryLogger();
            var projectPath = Path.GetFullPath(@"Project.vcxproj");
            fileSystem.AddFile(projectPath, new("""
                <?xml version="1.0" encoding="utf-8"?>
                <Project DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
                    <ItemGroup Label="ProjectConfigurations">
                        <ProjectConfiguration Include="Debug|Win32">
                            <Configuration>Debug</Configuration>
                            <Platform>Win32</Platform>
                        </ProjectConfiguration>
                    </ItemGroup>
                    <ItemGroup>
                        <ClCompile Include="main.cpp" Condition="'$(MyProperty)' == 'true'" />
                    </ItemGroup>
                </Project>
                """));

            // Act
            var project = MSBuildProject.ParseProjectFile(projectPath, fileSystem, logger);

            // Assert
            Assert.Contains("main.cpp", project.SourceFiles);
            Assert.Contains("Condition on element ClCompile is ignored: '$(MyProperty)' == 'true'", logger.AllMessageText);
        }

        [Fact]
        public void Given_ElementIgnoredBecauseOfUnexpectedConditionFormat_When_Parsed_Then_WarningIncludesElementNameAndConditionValue()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);
            var logger = new InMemoryLogger();
            var projectPath = Path.GetFullPath(@"Project.vcxproj");
            fileSystem.AddFile(projectPath, new("""
                <?xml version="1.0" encoding="utf-8"?>
                <Project DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
                    <ItemGroup Label="ProjectConfigurations">
                        <ProjectConfiguration Include="Debug|Win32">
                            <Configuration>Debug</Configuration>
                            <Platform>Win32</Platform>
                        </ProjectConfiguration>
                    </ItemGroup>
                    <ItemDefinitionGroup Condition="'$(Platform)' == 'Win32'">
                        <ClCompile>
                            <PreprocessorDefinitions>IGNORED_DEFINITION</PreprocessorDefinitions>
                        </ClCompile>
                    </ItemDefinitionGroup>
                </Project>
                """));

            // Act
            var project = MSBuildProject.ParseProjectFile(projectPath, fileSystem, logger);

            // Assert
            Assert.Empty(project.PreprocessorDefinitions.GetEffectiveValue(new("Debug|Win32")));
            Assert.Contains("Ignoring element ItemDefinitionGroup because its Condition has an unexpected format: '$(Platform)' == 'Win32'", logger.AllMessageText);
        }
    }
}
