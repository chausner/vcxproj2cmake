using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class TargetNameTests
    {
        [Fact]
        public void Given_TargetNameSetInAllConfigs_When_Converted_Then_SetTargetPropertiesOutputNameAdded()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithProperty("Debug", "Win32", "TargetName", "CustomName")
                .WithProperty("Release", "Win32", "TargetName", "CustomName")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains(
                """
                set_target_properties(Project PROPERTIES
                    OUTPUT_NAME CustomName
                )
                """,
                cmake);
        }

        [Fact]
        public void Given_DifferentTargetNamesSetInConfigs_When_Converted_Then_OutputNameUsesGeneratorExpression()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithProperty("Debug", "Win32", "TargetName", "CustomNameDebug")
                .WithProperty("Release", "Win32", "TargetName", "CustomNameRelease")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains(
                """
                set_target_properties(Project PROPERTIES
                    OUTPUT_NAME $<$<CONFIG:Debug>:CustomNameDebug>$<$<CONFIG:Release>:CustomNameRelease>
                )
                """,
                cmake);
        }
    }
}
