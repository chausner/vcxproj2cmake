using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class CxxModulesTests
    {
        [Fact]
        public void Given_ProjectWithCxxModuleFiles_When_Converted_Then_TargetSourcesListsModulesAsCxxModuleFileSet()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithItems("ClCompile",
                    @"src\main.cpp",
                    @"modules\core.ixx",
                    @"modules\math.cppm",
                    @"modules\net.CXXM",
                    @"modules\graphics.ccm",
                    @"modules\util.mpp",
                    @"modules\detail.mxx")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains("""
                target_sources(Project
                    PRIVATE
                        src/main.cpp
                )
                """, cmake);
            Assert.Contains("""
                target_sources(Project
                    PRIVATE
                        FILE_SET CXX_MODULES
                        FILES
                            modules/core.ixx
                            modules/detail.mxx
                            modules/graphics.ccm
                            modules/math.cppm
                            modules/net.CXXM
                            modules/util.mpp
                )
                """, cmake);
        }

        [Fact]
        public void Given_ProjectWithOnlyCxxModuleFiles_When_Converted_Then_TargetSourcesOnlyListsCxxModuleFileSet()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithItems("ClCompile", @"modules\core.ixx")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains("""
                target_sources(Project
                    PRIVATE
                        FILE_SET CXX_MODULES
                        FILES
                            modules/core.ixx
                )
                """, cmake);
            Assert.DoesNotContain("""
                target_sources(Project
                    PRIVATE
                        modules/core.ixx
                )
                """, cmake);
        }
    }
}
