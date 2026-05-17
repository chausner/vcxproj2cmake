using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class OpenMPSupportTests
    {
        [Fact]
        public void Given_OpenMPEnabledForAllConfigs_When_Converted_Then_LibraryAndPackageAdded()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithClCompileSetting("OpenMPSupport", "true")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains("find_package(OpenMP REQUIRED)", cmake);
            Assert.Contains("""
                target_link_libraries(Project
                    PRIVATE
                        OpenMP::OpenMP_CXX
                )
                """, cmake);
        }

        [Fact]
        public void Given_OpenMPEnabledOnlyForDebug_When_Converted_Then_LibraryUsesGeneratorExpression()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithClCompileSetting("OpenMPSupport", debugValue: "true", releaseValue: "false")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains("find_package(OpenMP REQUIRED)", cmake);
            Assert.Contains("""
                target_link_libraries(Project
                    PRIVATE
                        $<$<CONFIG:Debug>:OpenMP::OpenMP_CXX>
                )
                """, cmake);
        }

        [Fact]
        public void Given_OpenMPDisabled_When_Converted_Then_NoPackageOrLibraryAdded()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithClCompileSetting("OpenMPSupport", "false")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.DoesNotContain("find_package(OpenMP REQUIRED)", cmake);
            Assert.DoesNotContain("OpenMP::OpenMP_CXX", cmake);
            Assert.DoesNotContain("target_link_libraries(Project", cmake);
        }
    }
}
