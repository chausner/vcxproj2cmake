using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class IncludeHeadersTests
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
        public void Given_ProjectWithSourcesAndHeaders_When_IncludeHeadersIsFalse_Then_TargetSourcesDoesNotListHeaders()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProject("""
                <ItemGroup>
                    <ClCompile Include="src\main.cpp" />
                </ItemGroup>
                <ItemGroup>
                    <ClInclude Include="include\foo.h" />
                </ItemGroup>
                """)));

            var converter = new Converter(fileSystem, new InMemoryLogger());
            converter.Convert(projectFiles: [new FileInfo(@"Project.vcxproj")], includeHeaders: false);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("""                
                target_sources(Project
                    PRIVATE
                        src/main.cpp
                )
                """, cmake);
            Assert.DoesNotContain("include/foo.h", cmake);
        }

        [Fact]
        public void Given_ProjectWithSourcesAndHeaders_When_IncludeHeadersIsTrue_Then_TargetSourcesListsHeaders()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProject("""
                <ItemGroup>
                    <ClCompile Include="src\main.cpp" />
                </ItemGroup>
                <ItemGroup>
                    <ClInclude Include="include\foo.h" />
                </ItemGroup>
                """)));

            var converter = new Converter(fileSystem, new InMemoryLogger());
            converter.Convert(projectFiles: [new FileInfo(@"Project.vcxproj")], includeHeaders: true);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("""                
                target_sources(Project
                    PRIVATE
                        include/foo.h
                        src/main.cpp
                )
                """, cmake);
        }

        [Fact]
        public void Given_ProjectWithQtMocHeaders_When_IncludeHeadersIsTrue_Then_TargetSourcesListsQtMocHeaders()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProject("""
                <ItemGroup>
                    <ClCompile Include="src\main.cpp" />
                </ItemGroup>
                <ItemGroup>
                    <QtMoc Include="include\moc.H" />
                    <QtMoc Include="include\moc.HpP" />
                    <QtMoc Include="include\moc.HxX" />
                    <QtMoc Include="include\moc.H++" />
                    <QtMoc Include="include\moc.Hh" />
                </ItemGroup>
                """)));

            var converter = new Converter(fileSystem, new InMemoryLogger());
            converter.Convert(projectFiles: [new FileInfo(@"Project.vcxproj")], includeHeaders: true);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("""                
                target_sources(Project
                    PRIVATE
                        include/moc.H
                        include/moc.H++
                        include/moc.Hh
                        include/moc.HpP
                        include/moc.HxX
                        src/main.cpp
                )
                """, cmake);
        }

        [Fact]
        public void Given_ProjectWithQtMocSourceFiles_When_IncludeHeadersIsTrue_Then_TargetSourcesDoesNOtListQtMocSources()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProject("""
                <ItemGroup>
                    <ClCompile Include="src\main.cpp" />
                    <QtMoc Include="src\moc.cpp" />
                </ItemGroup>
                """)));

            var converter = new Converter(fileSystem, new InMemoryLogger());
            converter.Convert(projectFiles: [new FileInfo(@"Project.vcxproj")], includeHeaders: true);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("""                
                target_sources(Project
                    PRIVATE
                        src/main.cpp
                )
                """, cmake);
        }
    }
}

