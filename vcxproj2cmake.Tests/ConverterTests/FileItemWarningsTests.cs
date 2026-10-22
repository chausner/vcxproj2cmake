using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class FileItemWarningsTests
    {
        [Fact]
        public void Given_ProjectWithFileLevelMSBuildSettings_When_Converted_Then_LogsWarning()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);
            var expectedFilePath = PathUtils.NormalizePathSeparators(@"src\main.cpp");
            fileSystem.AddFile(
                @"Project.vcxproj",
                new MockFileData(TestData.Project()
                    .WithRawXml("""
                        <ItemGroup>
                            <ClCompile Include="src\main.cpp">
                                <ExcludeFromBuild>true</ExcludeFromBuild>
                                <PrecompiledHeader>NotUsing</PrecompiledHeader>
                            </ClCompile>
                        </ItemGroup>
                        """)
                    .Build()));

            var logger = new InMemoryLogger();
            var converter = new Converter(fileSystem, logger);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")],
                dryRun: true);

            // Assert
            Assert.Contains(
                $"File-level MSBuild settings are unsupported and will not be processed: {expectedFilePath} (ExcludeFromBuild, PrecompiledHeader)",
                logger.AllMessageText);
        }

        [Fact]
        public void Given_ProjectWithoutFileLevelMSBuildSettings_When_Converted_Then_LogsNoWarning()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);
            fileSystem.AddFile(
                @"Project.vcxproj",
                new MockFileData(TestData.Project()
                    .WithItems("ClCompile", @"src\main.cpp")
                    .WithItems("ClInclude", @"include\main.h")
                    .Build()));

            var logger = new InMemoryLogger();
            var converter = new Converter(fileSystem, logger);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")],
                dryRun: true);

            // Assert
            Assert.DoesNotContain(
                "File-level MSBuild settings are unsupported and will not be processed",
                logger.AllMessageText);
        }
    }
}
