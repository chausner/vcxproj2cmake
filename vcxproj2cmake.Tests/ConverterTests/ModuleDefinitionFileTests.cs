using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class ModuleDefinitionFileTests
    {
        [Fact]
        public void Given_ProjectWithModuleDefinitionFile_When_Converted_Then_TargetSourcesListsDefFile()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithLinkSetting("ModuleDefinitionFile", "project.def")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(projectFiles: [new(@"Project.vcxproj")]);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("""
                target_sources(Project
                    PRIVATE
                        project.def
                )
                """.Trim(), cmake);
        }

        [Fact]
        public void Given_ProjectWithConfigurationSpecificModuleDefinitionFile_When_Converted_Then_TargetSourcesUsesGeneratorExpressions()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithLinkSetting("ModuleDefinitionFile", debugValue: "project_debug.def", releaseValue: "project_release.def")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(projectFiles: [new(@"Project.vcxproj")]);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("""
                target_sources(Project
                    PRIVATE
                        $<$<CONFIG:Debug>:project_debug.def>
                        $<$<CONFIG:Release>:project_release.def>
                )
                """.Trim(), cmake);
        }
    }
}
