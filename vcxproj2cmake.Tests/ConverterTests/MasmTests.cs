using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class MasmTests
    {
        [Fact]
        public void Given_ProjectWithMasm_When_Converted_Then_TargetSourcesListsAsmFile()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithItems("ClCompile", @"src\main.cpp")
                .WithItems("MASM", @"src\assembler.asm")
                .Build()));

            var converter = new Converter(fileSystem, new InMemoryLogger());

            // Act
            converter.Convert(projectFiles: [new FileInfo(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains("""
                target_sources(Project
                    PRIVATE
                        src/assembler.asm
                        src/main.cpp
                )
                """, cmake);
        }
    }
}
