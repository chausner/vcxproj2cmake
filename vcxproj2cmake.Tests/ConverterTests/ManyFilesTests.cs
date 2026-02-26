using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class ManyFilesTests
    {
        [Fact]
        public void Given_ProjectWithManySources_When_Converted_Then_NoErrorOccurs()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            var sources = Enumerable.Range(1, 1500).Select(i => $"Source{i}.cpp").ToArray();
            fileSystem.AddFile(@"Project.vcxproj", new(TestData.CreateProjectWithSources(sources)));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            foreach (var source in sources)            
                Assert.Contains(source, cmake);            
        }
    }
}
