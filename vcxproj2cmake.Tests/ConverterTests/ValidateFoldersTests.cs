using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class ValidateFoldersTests
    {
        [Fact]
        public void Given_TwoProjectsInSameDirectory_When_Converted_Then_Throws()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(Path.Combine("App", "Project1.vcxproj"), new(TestData.EmptyProject));
            fileSystem.AddFile(Path.Combine("App", "Project2.vcxproj"), new(TestData.EmptyProject));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act & Assert
            var ex = Assert.Throws<CatastrophicFailureException>(() =>
                converter.Convert(
                    projectFiles: [new(Path.Combine("App", "Project1.vcxproj")), new(Path.Combine("App", "Project2.vcxproj"))]));
            Assert.Contains("contains two or more projects", ex.Message);
            Assert.Contains(Path.GetFullPath("App"), ex.Message);
        }

        [Fact]
        public void Given_SolutionFileAndProjectInSameDirectory_When_Converted_Then_Throws()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile("Project.vcxproj", new(TestData.EmptyProject));
            fileSystem.AddFile("Solution.sln", new("""
                Microsoft Visual Studio Solution File, Format Version 12.00
                # Visual Studio Version 17
                # MinimumVisualStudioVersion = 10.0.40219.1
                Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Project", "Project.vcxproj", "{11111111-1111-1111-1111-111111111111}"
                EndProject
            """));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act & Assert
            var ex = Assert.Throws<CatastrophicFailureException>(() =>
                converter.Convert(
                    solutionFile: new("Solution.sln")));
            Assert.Contains("The solution file and at least one project file are located in the same directory", ex.Message);
        }
    }
}
