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
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.CreateProjectWithClCompileProperty("OpenMPSupport", "true", "true")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("find_package(OpenMP REQUIRED)", cmake);
            Assert.Contains("""
                target_link_libraries(Project
                    PUBLIC
                        OpenMP::OpenMP_CXX
                )
                """, cmake);
        }

        [Fact]
        public void Given_OpenMPEnabledOnlyForDebug_When_Converted_Then_LibraryUsesGeneratorExpression()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.CreateProjectWithClCompileProperty("OpenMPSupport", "true", "false")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("find_package(OpenMP REQUIRED)", cmake);
            Assert.Contains("""
                target_link_libraries(Project
                    PUBLIC
                        $<$<CONFIG:Debug>:OpenMP::OpenMP_CXX>
                )
                """, cmake);
        }

        [Fact]
        public void Given_OpenMPDisabled_When_Converted_Then_NoPackageOrLibraryAdded()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.CreateProjectWithClCompileProperty("OpenMPSupport", "false", "false")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.DoesNotContain("find_package(OpenMP REQUIRED)", cmake);
            Assert.DoesNotContain("OpenMP::OpenMP_CXX", cmake);
            Assert.DoesNotContain("target_link_libraries(Project", cmake);
        }
    }
}
