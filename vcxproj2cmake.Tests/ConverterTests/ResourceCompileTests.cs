using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class ResourceCompileTests
    {
        [Fact]
        public void Given_ProjectWithResourceCompile_When_Converted_Then_TargetSourcesListsResourceFile()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.CreateProjectWithItemGroups("""
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
