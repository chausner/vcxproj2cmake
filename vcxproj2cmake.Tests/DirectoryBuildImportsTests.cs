using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public class DirectoryBuildImportsTests
{
    [Fact]
    public void GivenCppPropsImportInImportGroup_WhenDirectoryBuildPropsExists_ThenUsesNearestDirectoryBuildProps()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

        var projectPath = Path.Combine("src", "app", "app.vcxproj");
        fileSystem.AddFile(projectPath, new MockFileData(ProjectWithPropsImportInGroup));

        fileSystem.AddFile(Path.Combine("Directory.Build.props"), new MockFileData(DirectoryBuildPropsRoot));
        fileSystem.AddFile(Path.Combine("src", "Directory.Build.props"), new MockFileData(DirectoryBuildPropsNearest));

        // Act
        var project = MSBuildProject.ParseProjectFile(Path.GetFullPath(projectPath), fileSystem, NullLogger.Instance);

        // Assert
        var config = project.ProjectConfigurations[0];
        var warningLevel = project.WarningLevel.GetEffectiveValue(config);
        Assert.Equal("Level4", warningLevel);
    }

    [Fact]
    public void GivenCppTargetsImport_WhenDirectoryBuildTargetsExists_ThenIncludesTargetsSettings()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

        var projectPath = Path.Combine("src", "app", "app.vcxproj");
        fileSystem.AddFile(projectPath, new MockFileData(ProjectWithTargetsImport));

        fileSystem.AddFile(Path.Combine("src", "Directory.Build.targets"), new MockFileData(DirectoryBuildTargets));

        // Act
        var project = MSBuildProject.ParseProjectFile(Path.GetFullPath(projectPath), fileSystem, NullLogger.Instance);

        // Assert
        var config = project.ProjectConfigurations[0];
        var treatWarningAsError = project.TreatWarningAsError.GetEffectiveValue(config);
        Assert.Equal("true", treatWarningAsError);
    }

    const string ProjectWithPropsImportInGroup = """
        <?xml version="1.0" encoding="utf-8"?>
        <Project DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
            <ItemGroup Label="ProjectConfigurations">
                <ProjectConfiguration Include="Debug|Win32">
                    <Configuration>Debug</Configuration>
                    <Platform>Win32</Platform>
                </ProjectConfiguration>
                <ProjectConfiguration Include="Release|Win32">
                    <Configuration>Release</Configuration>
                    <Platform>Win32</Platform>
                </ProjectConfiguration>
            </ItemGroup>
            <ImportGroup Label="Props">
                <Import Project="$(VCTargetsPath)\Microsoft.Cpp.props" />
            </ImportGroup>
            <Import Project="$(VCTargetsPath)\Microsoft.Cpp.targets" />
        </Project>
        """;

    const string ProjectWithTargetsImport = """
        <?xml version="1.0" encoding="utf-8"?>
        <Project DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
            <ItemGroup Label="ProjectConfigurations">
                <ProjectConfiguration Include="Debug|Win32">
                    <Configuration>Debug</Configuration>
                    <Platform>Win32</Platform>
                </ProjectConfiguration>
                <ProjectConfiguration Include="Release|Win32">
                    <Configuration>Release</Configuration>
                    <Platform>Win32</Platform>
                </ProjectConfiguration>
            </ItemGroup>
            <ItemDefinitionGroup>
                <ClCompile>
                    <TreatWarningAsError>false</TreatWarningAsError>
                </ClCompile>
            </ItemDefinitionGroup>
            <Import Project="$(VCTargetsPath)\Microsoft.Cpp.targets" />
        </Project>
        """;

    const string DirectoryBuildPropsRoot = """
        <?xml version="1.0" encoding="utf-8"?>
        <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
            <ItemDefinitionGroup>
                <ClCompile>
                    <WarningLevel>Level1</WarningLevel>
                </ClCompile>
            </ItemDefinitionGroup>
        </Project>
        """;

    const string DirectoryBuildPropsNearest = """
        <?xml version="1.0" encoding="utf-8"?>
        <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
            <ItemDefinitionGroup>
                <ClCompile>
                    <WarningLevel>Level4</WarningLevel>
                </ClCompile>
            </ItemDefinitionGroup>
        </Project>
        """;

    const string DirectoryBuildTargets = """
        <?xml version="1.0" encoding="utf-8"?>
        <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
            <ItemDefinitionGroup>
                <ClCompile>
                    <TreatWarningAsError>true</TreatWarningAsError>
                </ClCompile>
            </ItemDefinitionGroup>
        </Project>
        """;
}
