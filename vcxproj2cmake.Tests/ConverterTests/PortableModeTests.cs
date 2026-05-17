using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class PortableModeTests
    {
        [Fact]
        public void Given_PortableModeAndProjectWithoutSources_When_Converted_Then_SettingsAreGuardedUsingCXX_COMPILER_ID()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithClCompileSetting("AdditionalOptions", "foo")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")],
                portable: true);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains("""
                target_compile_options(Project
                    PRIVATE
                        $<$<CXX_COMPILER_ID:MSVC>:foo>
                )
                """.TrimEnd(), cmake);
        }

        [Fact]
        public void Given_PortableModeAndProjectWithOnlyCFiles_When_Converted_Then_SettingsAreGuardedUsingC_COMPILER_ID()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithClCompileSetting("AdditionalOptions", "foo")
                .WithItems("ClCompile", "main.c")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")],
                portable: true);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains("""
                target_compile_options(Project
                    PRIVATE
                        $<$<C_COMPILER_ID:MSVC>:foo>
                )
                """.TrimEnd(), cmake);
        }

        [Fact]
        public void Given_PortableModeAndProjectWithOnlyCppFiles_When_Converted_Then_SettingsAreGuardedUsingCXX_COMPILER_ID()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithClCompileSetting("AdditionalOptions", "foo")
                .WithItems("ClCompile", "main.cpp")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")],
                portable: true);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains("""
                target_compile_options(Project
                    PRIVATE
                        $<$<CXX_COMPILER_ID:MSVC>:foo>
                )
                """.TrimEnd(), cmake);
        }

        [Fact]
        public void Given_PortableModeAndProjectWithCAndCppFiles_When_Converted_Then_SettingsAreGuardedUsingCXX_COMPILER_ID()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithClCompileSetting("AdditionalOptions", "foo")
                .WithItems("ClCompile", "main.c", "main.cpp")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")],
                portable: true);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains("""
                target_compile_options(Project
                    PRIVATE
                        $<$<CXX_COMPILER_ID:MSVC>:foo>
                )
                """.TrimEnd(), cmake);
        }
    }
}
