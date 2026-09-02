using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class OutDirTests
    {
        [Fact]
        public void Given_OutDirSameForAllConfigs_When_Converted_Then_RuntimeOutputDirectoryIsSet()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithProperty("ConfigurationType", "Application")
                .WithProperty("OutDir", @"C:\Out\")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            
            Assert.Contains("""
                set_target_properties(Project PROPERTIES
                    RUNTIME_OUTPUT_DIRECTORY C:/Out
                )
                """.TrimEnd(), cmake);
        }

        [Fact]
        public void Given_OutDirDifferentPerConfig_When_Converted_Then_RuntimeOutputDirectoryUsesGeneratorExpression()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithProperty("ConfigurationType", "Application")
                .WithProperty("Debug", "Win32", "OutDir", @"bin\Debug\")
                .WithProperty("Release", "Win32", "OutDir", @"bin\Release\")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains("""
                set_target_properties(Project PROPERTIES
                    RUNTIME_OUTPUT_DIRECTORY "$<$<CONFIG:Debug>:bin/Debug>$<$<CONFIG:Release>:bin/Release>"
                )
                """.TrimEnd(), cmake);
        }

        [Fact]
        public void Given_OutDirForDynamicLibrary_When_Converted_Then_RuntimeLibraryAndArchiveOutputDirectoryAreSet()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithProperty("ConfigurationType", "DynamicLibrary")
                .WithProperty("OutDir", @"out\bin\")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains("""
                set_target_properties(Project PROPERTIES
                    RUNTIME_OUTPUT_DIRECTORY out/bin
                    LIBRARY_OUTPUT_DIRECTORY out/bin
                    ARCHIVE_OUTPUT_DIRECTORY out/bin
                )
                """.TrimEnd(), cmake);
        }

        [Fact]
        public void Given_OutDirForStaticLibrary_When_Converted_Then_ArchiveOutputDirectoryIsSet()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithProperty("ConfigurationType", "StaticLibrary")
                .WithProperty("OutDir", @"out\lib\")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains("""
                set_target_properties(Project PROPERTIES
                    ARCHIVE_OUTPUT_DIRECTORY out/lib
                )
                """.TrimEnd(), cmake);
        }
    }
}
