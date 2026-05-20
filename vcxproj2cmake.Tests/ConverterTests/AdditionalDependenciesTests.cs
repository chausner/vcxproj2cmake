using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class AdditionalDependenciesTests
    {
        [Fact]
        public void Given_ProjectWithAdditionalDependencies_When_Converted_Then_LibrariesAreLinked()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithLinkSetting("AdditionalDependencies", "Foo.lib;Bar.lib;%(AdditionalDependencies)")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains(
                """
                target_link_libraries(Project
                    PRIVATE
                        Foo.lib
                        Bar.lib
                )
                """.Trim(),
                cmake);
        }

        [Fact]
        public void Given_ProjectWithConfigSpecificAdditionalDependencies_When_Converted_Then_GeneratorExpressionsUsed()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithLinkSetting("AdditionalDependencies", debugValue: "Foo_d.lib;%(AdditionalDependencies)", releaseValue: "Foo.lib;%(AdditionalDependencies)")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains(
                """
                target_link_libraries(Project
                    PRIVATE
                        "$<$<CONFIG:Debug>:Foo_d.lib>"
                        "$<$<CONFIG:Release>:Foo.lib>"
                )
                """.Trim(),
                cmake);
        }
    }
}
