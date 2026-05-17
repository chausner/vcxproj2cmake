using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class MfcSupportTests
    {
        [Fact]
        public void Given_ProjectWithStaticMfc_When_Converted_Then_CMakeMfcFlagSetTo1AndAfxdllDefinitionIsNotAdded()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithProperty("UseOfMfc", "Static")
                .Build()));

            // Act
            var converter = new Converter(fileSystem, NullLogger.Instance);
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains("""
                set_target_properties(Project PROPERTIES
                    CMAKE_MFC_FLAG 1
                )
                """.TrimEnd(), cmake);
            Assert.DoesNotContain("_AFXDLL", cmake);
        }

        [Fact]
        public void Given_ProjectWithSharedMfc_When_Converted_Then_CMakeMfcFlagSetTo2AndAfxdllDefinitionAdded()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithProperty("UseOfMfc", "Dynamic")
                .Build()));

            // Act
            var converter = new Converter(fileSystem, NullLogger.Instance);
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains("""
                set_target_properties(Project PROPERTIES
                    CMAKE_MFC_FLAG 2
                )
                """.TrimEnd(), cmake);
            Assert.Contains("""
                target_compile_definitions(Project
                    PRIVATE
                        _AFXDLL
                )
                """.TrimEnd(), cmake);
        }

        [Fact]
        public void Given_ProjectWithSharedMfcInRelease_When_Converted_Then_GeneratorExpressionsAreUsedForCMakeMfcFlagAndAfxdll()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithProperty("Debug", "Win32", "UseOfMfc", "false")
                .WithProperty("Release", "Win32", "UseOfMfc", "Dynamic")
                .Build()));

            // Act
            var converter = new Converter(fileSystem, NullLogger.Instance);
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains("""
                set_target_properties(Project PROPERTIES
                    CMAKE_MFC_FLAG $<$<CONFIG:Debug>:0>$<$<CONFIG:Release>:2>
                )
                """.TrimEnd(), cmake);
            Assert.Contains("""
                target_compile_definitions(Project
                    PRIVATE
                        $<$<CONFIG:Release>:_AFXDLL>
                )
                """.TrimEnd(), cmake);
        }

        [Fact]
        public void Given_ProjectWithStaticMfcInDebugAndSharedMfcInRelease_When_Converted_Then_AfxdllDefinitionIsAddedOnlyForRelease()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithProperty("Debug", "Win32", "UseOfMfc", "Static")
                .WithProperty("Release", "Win32", "UseOfMfc", "Dynamic")
                .Build()));

            // Act
            var converter = new Converter(fileSystem, NullLogger.Instance);
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains("""
                set_target_properties(Project PROPERTIES
                    CMAKE_MFC_FLAG $<$<CONFIG:Debug>:1>$<$<CONFIG:Release>:2>
                )
                """.TrimEnd(), cmake);
            Assert.Contains("""
                target_compile_definitions(Project
                    PRIVATE
                        $<$<CONFIG:Release>:_AFXDLL>
                )
                """.TrimEnd(), cmake);
        }

        [Fact]
        public void Given_ProjectWithoutMfc_When_Converted_Then_NoMfcFlagOrAfxdllDefinitionAdded()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithProperty("UseOfMfc", "false")
                .Build()));

            // Act
            var converter = new Converter(fileSystem, NullLogger.Instance);
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.DoesNotContain("CMAKE_MFC_FLAG", cmake);
            Assert.DoesNotContain("_AFXDLL", cmake);
        }

        [Fact]
        public void Given_ProjectWithInvalidMfcValue_When_Converted_Then_Throws()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithProperty("UseOfMfc", "invalid")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act & Assert
            var ex = Assert.Throws<CatastrophicFailureException>(() =>
                converter.Convert(
                    projectFiles: [new(@"Project.vcxproj")]));

            Assert.Contains("Invalid value for UseOfMfc", ex.Message);
        }
    }
}
