using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class LinkerOptionsTests
    {
        [Fact]
        public void Given_AdditionalOptions_When_Converted_Then_OptionsAreWritten()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithLinkSetting("AdditionalOptions", "foo bar")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains("""
                target_link_options(Project
                    PRIVATE
                        foo
                        bar
                )
                """, cmake);
        }

        [Fact]
        public void Given_AdditionalOptionsAndPortableMode_When_Converted_Then_OptionsAreGuarded()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithLinkSetting("AdditionalOptions", "foo bar")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")],
                portable: true);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains("""
                target_link_options(Project
                    PRIVATE
                        "$<$<CXX_COMPILER_ID:MSVC>:foo>"
                        "$<$<CXX_COMPILER_ID:MSVC>:bar>"
                )
                """, cmake);
        }
        
        [Fact]
        public void Given_AdditionalOptionsWithQuotedArguments_When_Converted_Then_QuotedArgumentsAreNotSplit()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithLinkSetting("AdditionalOptions", """/LIBPATH:&quot;C:\Third Party Libs&quot; /MANIFESTINPUT:&quot;C:\app manifest.xml&quot; /DEBUG""")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains("""
                target_link_options(Project
                    PRIVATE
                        "/LIBPATH:C:\\Third Party Libs"
                        "/MANIFESTINPUT:C:\\app manifest.xml"
                        /DEBUG
                )
                """.TrimEnd(), cmake);
        }

        [Fact]
        public void Given_AdditionalOptionsWithQuotedArgumentsAndPortableMode_When_Converted_Then_QuotedGuardedArgumentsAreNotSplit()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithLinkSetting("AdditionalOptions", """/LIBPATH:&quot;C:\Third Party Libs&quot; /MANIFESTINPUT:&quot;C:\app manifest.xml&quot; /DEBUG""")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")],
                portable: true);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains("""
                target_link_options(Project
                    PRIVATE
                        "$<$<CXX_COMPILER_ID:MSVC>:/LIBPATH:C:\\Third Party Libs>"
                        "$<$<CXX_COMPILER_ID:MSVC>:/MANIFESTINPUT:C:\\app manifest.xml>"
                        "$<$<CXX_COMPILER_ID:MSVC>:/DEBUG>"
                )
                """.TrimEnd(), cmake);
        }
    }
}
