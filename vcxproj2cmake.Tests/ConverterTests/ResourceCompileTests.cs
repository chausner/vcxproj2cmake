using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class ResourceCompileTests
    {
        static string CreateProject(string itemGroupsXml)
            => $"""
                <?xml version="1.0" encoding="utf-8"?>
                <Project DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
                    <ItemGroup Label="ProjectConfigurations">
                        <ProjectConfiguration Include="Debug|Win32">
                            <Configuration>Debug</Configuration>
                            <Platform>Win32</Platform>
                        </ProjectConfiguration>
                    </ItemGroup>
                    <PropertyGroup>
                        <ConfigurationType>Application</ConfigurationType>
                    </PropertyGroup>
                    {itemGroupsXml}
                </Project>
                """;

        [Fact]
        public void Given_ProjectWithResourceCompile_When_Converted_Then_TargetSourcesListsResourceFile()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProject("""
                <ItemGroup>
                    <ClCompile Include="src\main.cpp" />
                    <ResourceCompile Include="app.rc" />
                </ItemGroup>
                """)));

            var converter = new Converter(fileSystem, new InMemoryLogger());
            converter.Convert(projectFiles: [new FileInfo(@"Project.vcxproj")]);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("""                
                target_sources(Project
                    PRIVATE
                        app.rc
                        src/main.cpp
                )
                """, cmake);
        }
    }
}
