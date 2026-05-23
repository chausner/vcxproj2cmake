using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public class MSBuildProjectImportTests
{
    [Fact]
    public void Given_ExplicitNestedImports_When_Parsed_Then_ImportedPropertiesAreAppliedTransitively()
    {
        // Arrange
        var fileSystem = CreateFileSystem();
        fileSystem.AddFile(@"Project.vcxproj", new(ProjectXml("""
            <Import Project="props\first.props" />
            """)));
        fileSystem.AddFile(@"props\first.props", new(ProjectXml("""
            <Import Project="nested.props" />
            """, includeProjectConfigurations: false)));
        fileSystem.AddFile(@"props\nested.props", new(ProjectXml("""
            <PropertyGroup>
                <TargetName>NestedTarget</TargetName>
            </PropertyGroup>
            """, includeProjectConfigurations: false)));

        // Act
        var project = MSBuildProject.ParseProjectFile(@"Project.vcxproj", fileSystem, NullLogger.Instance);

        // Assert
        Assert.Equal("NestedTarget", project.TargetName.GetEffectiveValue(project.ProjectConfigurations[0]));
    }

    [Fact]
    public void Given_ImportBetweenTwoPropertyGroups_When_Parsed_Then_LaterProjectPropertyWins()
    {
        // Arrange
        var fileSystem = CreateFileSystem();
        fileSystem.AddFile(@"Project.vcxproj", new(ProjectXml("""
            <PropertyGroup>
                <TargetName>BeforeImport</TargetName>
            </PropertyGroup>
            <Import Project="override.props" />
            <PropertyGroup>
                <TargetName>AfterImport</TargetName>
            </PropertyGroup>
            """)));
        fileSystem.AddFile(@"override.props", new(ProjectXml("""
            <PropertyGroup>
                <TargetName>ImportedTarget</TargetName>
            </PropertyGroup>
            """, includeProjectConfigurations: false)));

        // Act
        var project = MSBuildProject.ParseProjectFile(@"Project.vcxproj", fileSystem, NullLogger.Instance);

        // Assert
        Assert.Equal("AfterImport", project.TargetName.GetEffectiveValue(project.ProjectConfigurations[0]));
    }

    [Fact]
    public void Given_ImportAfterPropertyGroup_When_Parsed_Then_ImportedPropertyWins()
    {
        // Arrange
        var fileSystem = CreateFileSystem();
        fileSystem.AddFile(@"Project.vcxproj", new(ProjectXml("""
            <PropertyGroup>
                <TargetName>BeforeImport</TargetName>
            </PropertyGroup>
            <Import Project="override.props" />
            """)));
        fileSystem.AddFile(@"override.props", new(ProjectXml("""
            <PropertyGroup>
                <TargetName>ImportedTarget</TargetName>
            </PropertyGroup>
            """, includeProjectConfigurations: false)));

        // Act
        var project = MSBuildProject.ParseProjectFile(@"Project.vcxproj", fileSystem, NullLogger.Instance);

        // Assert
        Assert.Equal("ImportedTarget", project.TargetName.GetEffectiveValue(project.ProjectConfigurations[0]));
    }

    [Fact]
    public void Given_DirectoryBuildPropsFoundThroughCppProps_When_Parsed_Then_ItIsImportedAtCppPropsLocation()
    {
        // Arrange
        var fileSystem = CreateFileSystem();
        fileSystem.AddFile(@"Directory.Build.props", new(ProjectXml("""
            <PropertyGroup>
                <TargetName>DirectoryBuildTarget</TargetName>
            </PropertyGroup>
            """, includeProjectConfigurations: false)));
        fileSystem.AddFile(@"src\Project.vcxproj", new(ProjectXml("""
            <Import Project="$(VCTargetsPath)\Microsoft.Cpp.props" />
            <PropertyGroup>
                <TargetName>ProjectTarget</TargetName>
            </PropertyGroup>
            """)));

        // Act
        var project = MSBuildProject.ParseProjectFile(@"src\Project.vcxproj", fileSystem, NullLogger.Instance);

        // Assert
        Assert.Equal("ProjectTarget", project.TargetName.GetEffectiveValue(project.ProjectConfigurations[0]));
    }

    [Fact]
    public void Given_DirectoryBuildTargetsFoundThroughCppTargets_When_Parsed_Then_ItIsImportedAtCppTargetsLocation()
    {
        // Arrange
        var fileSystem = CreateFileSystem();
        fileSystem.AddFile(@"Directory.Build.targets", new(ProjectXml("""
            <ItemDefinitionGroup>
                <Link>
                    <AdditionalLibraryDirectories>DirectoryBuildLib</AdditionalLibraryDirectories>
                </Link>
            </ItemDefinitionGroup>
            """, includeProjectConfigurations: false)));
        fileSystem.AddFile(@"src\Project.vcxproj", new(ProjectXml("""
            <ItemDefinitionGroup>
                <Link>
                    <AdditionalLibraryDirectories>ProjectLib</AdditionalLibraryDirectories>
                </Link>
            </ItemDefinitionGroup>
            <Import Project="$(VCTargetsPath)\Microsoft.Cpp.targets" />
            """)));

        // Act
        var project = MSBuildProject.ParseProjectFile(@"src\Project.vcxproj", fileSystem, NullLogger.Instance);

        // Assert
        Assert.Equal(["DirectoryBuildLib"], project.AdditionalLibraryDirectories.GetEffectiveValue(project.ProjectConfigurations[0]));
    }

    [Fact]
    public void Given_GetPathOfFileAboveInImportProjectAndCondition_When_Parsed_Then_ParentFileIsImported()
    {
        // Arrange
        var fileSystem = CreateFileSystem();
        fileSystem.AddFile(@"src\Directory.Build.targets", new(ProjectXml("""
            <PropertyGroup>
                <TargetName>ParentTarget</TargetName>
            </PropertyGroup>
            """, includeProjectConfigurations: false)));
        fileSystem.AddFile(@"src\app\Project.vcxproj", new(ProjectXml("""
            <Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Build.targets', '$(MSBuildThisFileDirectory)../'))"
                    Condition="'' != $([MSBuild]::GetPathOfFileAbove('Directory.Build.targets', '$(MSBuildThisFileDirectory)../'))" />
            """)));

        // Act
        var project = MSBuildProject.ParseProjectFile(@"src\app\Project.vcxproj", fileSystem, NullLogger.Instance);

        // Assert
        Assert.Equal("ParentTarget", project.TargetName.GetEffectiveValue(project.ProjectConfigurations[0]));
    }

    [Fact]
    public void Given_GetPathOfFileAboveStoredInProperty_When_Parsed_Then_PropertyImportIsResolved()
    {
        // Arrange
        var fileSystem = CreateFileSystem();
        fileSystem.AddFile(@"common.targets", new(ProjectXml("""
            <PropertyGroup>
                <TargetName>CommonTarget</TargetName>
            </PropertyGroup>
            """, includeProjectConfigurations: false)));
        fileSystem.AddFile(@"src\app\Project.vcxproj", new(ProjectXml("""
            <PropertyGroup>
                <CommonTargets>$([MSBuild]::GetPathOfFileAbove(common.targets))</CommonTargets>
            </PropertyGroup>
            <Import Project="$(CommonTargets)" Condition="exists('$(CommonTargets)')" />
            """)));

        // Act
        var project = MSBuildProject.ParseProjectFile(@"src\app\Project.vcxproj", fileSystem, NullLogger.Instance);

        // Assert
        Assert.Equal("CommonTarget", project.TargetName.GetEffectiveValue(project.ProjectConfigurations[0]));
    }

    [Fact]
    public void Given_GetPathOfFileAboveWithoutCondition_When_Parsed_Then_FileIsImported()
    {
        // Arrange
        var fileSystem = CreateFileSystem();
        fileSystem.AddFile(@"global.props", new(ProjectXml("""
            <PropertyGroup>
                <TargetName>GlobalTarget</TargetName>
            </PropertyGroup>
            """, includeProjectConfigurations: false)));
        fileSystem.AddFile(@"src\Project.vcxproj", new(ProjectXml("""
            <Import Project="$([MSBuild]::GetPathOfFileAbove('global.props', '$(MSBuildThisFileDirectory)../'))" />
            """)));

        // Act
        var project = MSBuildProject.ParseProjectFile(@"src\Project.vcxproj", fileSystem, NullLogger.Instance);

        // Assert
        Assert.Equal("GlobalTarget", project.TargetName.GetEffectiveValue(project.ProjectConfigurations[0]));
    }

    [Fact]
    public void Given_DirectoryBuildPropsImportsSameFileAbove_When_Parsed_Then_ParentFileIsResolvedFromImportedFileDirectory()
    {
        // Arrange
        var fileSystem = CreateFileSystem();
        fileSystem.AddFile(@"Directory.Build.props", new(ProjectXml("""
            <PropertyGroup>
                <TargetName>RootDirectoryBuildTarget</TargetName>
            </PropertyGroup>
            """, includeProjectConfigurations: false)));
        fileSystem.AddFile(@"src\Directory.Build.props", new(ProjectXml("""
            <Import Project="$([MSBuild]::GetPathOfFileAbove($(MSBuildThisFile), $(MSBuildThisFileDirectory)..))"
                    Condition="Exists($([MSBuild]::GetPathOfFileAbove($(MSBuildThisFile), $(MSBuildThisFileDirectory)..)))" />
            """, includeProjectConfigurations: false)));
        fileSystem.AddFile(@"src\app\Project.vcxproj", new(ProjectXml("""
            <Import Project="$(VCTargetsPath)\Microsoft.Cpp.Default.props" />
            """)));

        // Act
        var project = MSBuildProject.ParseProjectFile(@"src\app\Project.vcxproj", fileSystem, NullLogger.Instance);

        // Assert
        Assert.Equal("RootDirectoryBuildTarget", project.TargetName.GetEffectiveValue(project.ProjectConfigurations[0]));
    }

    static MockFileSystem CreateFileSystem()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);
        return fileSystem;
    }

    static string ProjectXml(string body, bool includeProjectConfigurations = true)
    {
        var projectConfigurations = includeProjectConfigurations
            ? """
                <ItemGroup Label="ProjectConfigurations">
                    <ProjectConfiguration Include="Debug|Win32">
                        <Configuration>Debug</Configuration>
                        <Platform>Win32</Platform>
                    </ProjectConfiguration>
                </ItemGroup>
                """
            : string.Empty;

        return $$"""
            <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
            {{projectConfigurations}}
            {{body}}
            </Project>
            """;
    }
}
