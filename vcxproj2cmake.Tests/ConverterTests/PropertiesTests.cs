using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class PropertiesTests
    {
        [Fact]
        public void Given_TreatWarningAsErrorEnabledForAllConfigs_When_Converted_Then_CompileWarningAsErrorPropertyIsSet()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.CreateProjectWithClCompileProperty("TreatWarningAsError", "true", "true")));

            var converter = new Converter(fileSystem, NullLogger.Instance);
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("""
                set_target_properties(Project PROPERTIES
                    COMPILE_WARNING_AS_ERROR ON
                )
                """.TrimEnd(), cmake);
        }

        [Fact]
        public void Given_TreatWarningAsErrorEnabledConfigSpecific_When_Converted_Then_CompileWarningAsErrorPropertyIsSetWithGeneratorExpression()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.CreateProjectWithClCompileProperty("TreatWarningAsError", "true", "false")));

            var converter = new Converter(fileSystem, NullLogger.Instance);
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("""
                set_target_properties(Project PROPERTIES
                    COMPILE_WARNING_AS_ERROR "$<$<CONFIG:Debug>:ON>$<$<CONFIG:Release>:OFF>"
                )
                """.TrimEnd(), cmake);
        }
    }
}
