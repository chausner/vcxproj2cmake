using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class BuildEventTests
    {
        [Fact]
        public void Given_PreBuildAndPostBuildEvents_When_Converted_Then_AddCustomCommandsAreGenerated()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithRawXml("""
                    <ItemDefinitionGroup>
                        <PreBuildEvent>
                            <Command>echo Preparing $(ProjectName)</Command>
                        </PreBuildEvent>
                        <PostBuildEvent>
                            <Command>copy "$(TargetPath)" "$(ProjectDir)bin\$(TargetFileName)"</Command>
                        </PostBuildEvent>
                    </ItemDefinitionGroup>
                    """)
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            File.WriteAllText("CMakeLists.txt", cmake);

            Assert.Contains("""
                add_custom_command(TARGET Project PRE_BUILD
                    COMMAND cmd /C "echo Preparing ${PROJECT_NAME}"
                )
                """, cmake);
            Assert.Contains("""
                add_custom_command(TARGET Project POST_BUILD
                    COMMAND cmd /C "copy \"$<TARGET_FILE:Project>\" \"${CMAKE_CURRENT_SOURCE_DIR}/bin\\$<TARGET_FILE_NAME:Project>\""
                )
                """, cmake);
        }

        [Fact]
        public void Given_ConfigSpecificBuildEvents_When_Converted_Then_GeneratorExpressionsAreUsed()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithRawXml("""
                    <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Debug|Win32'">
                        <PostBuildEvent>
                            <Command>echo Debug</Command>
                        </PostBuildEvent>
                    </ItemDefinitionGroup>
                    <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Release|Win32'">
                        <PostBuildEvent>
                            <Command>echo Release</Command>
                        </PostBuildEvent>
                    </ItemDefinitionGroup>
                    """)
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains("""
                add_custom_command(TARGET Project POST_BUILD
                    COMMAND cmd /C "$<$<CONFIG:Debug>:echo Debug>"
                    COMMAND cmd /C "$<$<CONFIG:Release>:echo Release>"
                )
                """, cmake);
        }
    }
}
