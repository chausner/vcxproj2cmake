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
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithItems("ClCompile", @"src\main.cpp")
                .WithItems("ResourceCompile", @"app.rc")
                .Build()));

            var converter = new Converter(fileSystem, new InMemoryLogger());

            // Act
            converter.Convert(projectFiles: [new FileInfo(@"Project.vcxproj")]);

            // Assert
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
