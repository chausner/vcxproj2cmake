using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class BuildEventTests
    {
        [Fact]
        public void Given_BuildEvents_When_Converted_Then_AddCustomCommandsAreGenerated()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithRawXml("""
                    <ItemDefinitionGroup>
                        <PreBuildEvent>
                            <Command>echo Building $(ProjectName)</Command>
                        </PreBuildEvent>
                        <PreLinkEvent>
                            <Command>echo Linking $(ProjectName)</Command>
                        </PreLinkEvent>
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

            Assert.Contains("""
                add_custom_command(TARGET Project PRE_BUILD
                    COMMAND cmd /C "echo Building ${PROJECT_NAME}"
                )
                """, cmake);
            Assert.Contains("""
                add_custom_command(TARGET Project PRE_LINK
                    COMMAND cmd /C "echo Linking ${PROJECT_NAME}"
                )
                """, cmake);
            Assert.Contains("""
                add_custom_command(TARGET Project POST_BUILD
                    COMMAND cmd /C "copy \"$<TARGET_FILE:Project>\" \"${CMAKE_CURRENT_SOURCE_DIR}/bin\\$<TARGET_FILE_NAME:Project>\""
                )
                """, cmake);
        }

        [Fact]
        public void Given_BuildEventsWithMessages_When_Converted_Then_AddCustomCommandsWithCommentsAreGenerated()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithRawXml("""
                    <ItemDefinitionGroup>
                        <PreBuildEvent>
                            <Command>echo Building $(ProjectName)</Command>
                            <Message>Building $(ProjectName)</Message>
                        </PreBuildEvent>
                        <PreLinkEvent>
                            <Command>echo Linking $(ProjectName)</Command>
                            <Message>Linking $(ProjectName)</Message>
                        </PreLinkEvent>
                        <PostBuildEvent>
                            <Command>copy "$(TargetPath)" "$(ProjectDir)bin\$(TargetFileName)"</Command>
                            <Message>Copying $(TargetFileName)</Message>
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
                add_custom_command(TARGET Project PRE_BUILD
                    COMMAND cmd /C "echo Building ${PROJECT_NAME}"
                    COMMENT "Building ${PROJECT_NAME}"
                )
                """, cmake);
            Assert.Contains("""
                add_custom_command(TARGET Project PRE_LINK
                    COMMAND cmd /C "echo Linking ${PROJECT_NAME}"
                    COMMENT "Linking ${PROJECT_NAME}"
                )
                """, cmake);
            Assert.Contains("""
                add_custom_command(TARGET Project POST_BUILD
                    COMMAND cmd /C "copy \"$<TARGET_FILE:Project>\" \"${CMAKE_CURRENT_SOURCE_DIR}/bin\\$<TARGET_FILE_NAME:Project>\""
                    COMMENT "Copying $<TARGET_FILE_NAME:Project>"
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
                        <PreBuildEvent>
                            <Command>echo Building Debug</Command>
                        </PreBuildEvent>
                        <PreLinkEvent>
                            <Command>echo Linking Debug</Command>
                        </PreLinkEvent>
                        <PostBuildEvent>
                            <Command>echo Completed Debug</Command>
                        </PostBuildEvent>
                    </ItemDefinitionGroup>
                    <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Release|Win32'">
                        <PreBuildEvent>
                            <Command>echo Building Release</Command>
                        </PreBuildEvent>
                        <PreLinkEvent>
                            <Command>echo Linking Release</Command>
                        </PreLinkEvent>
                        <PostBuildEvent>
                            <Command>echo Completed Release</Command>
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
                add_custom_command(TARGET Project PRE_BUILD
                    COMMAND cmd /C "$<$<CONFIG:Debug>:echo Building Debug>"
                    COMMAND cmd /C "$<$<CONFIG:Release>:echo Building Release>"
                )
                """, cmake);
            Assert.Contains("""
                add_custom_command(TARGET Project PRE_LINK
                    COMMAND cmd /C "$<$<CONFIG:Debug>:echo Linking Debug>"
                    COMMAND cmd /C "$<$<CONFIG:Release>:echo Linking Release>"
                )
                """, cmake);
            Assert.Contains("""
                add_custom_command(TARGET Project POST_BUILD
                    COMMAND cmd /C "$<$<CONFIG:Debug>:echo Completed Debug>"
                    COMMAND cmd /C "$<$<CONFIG:Release>:echo Completed Release>"
                )
                """, cmake);
        }

        [Fact]
        public void Given_PreBuildEventWithConfigSpecificPreBuildEventUseInBuild_When_Converted_Then_PreBuildEventUseInBuildIsRespected()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithRawXml("""
                    <ItemDefinitionGroup>
                        <PreBuildEvent>
                            <Command>echo Building $(ProjectName)</Command>
                        </PreBuildEvent>
                    </ItemDefinitionGroup>
                    <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Debug|Win32'">
                        <PreBuildEventUseInBuild>false</PreBuildEventUseInBuild>                      
                    </PropertyGroup>
                    """)
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains("""
                add_custom_command(TARGET Project PRE_BUILD
                    COMMAND "$<$<CONFIG:Release>:cmd /C echo Building ${PROJECT_NAME}>"
                )
                """, cmake);
        }

        [Fact]
        public void Given_PreLinkEventWithConfigSpecificPreLinkEventUseInBuild_When_Converted_Then_PreLinkEventUseInBuildIsRespected()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithRawXml("""
                    <ItemDefinitionGroup>
                        <PreLinkEvent>
                            <Command>echo Linking $(ProjectName)</Command>
                        </PreLinkEvent>
                    </ItemDefinitionGroup>
                    <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Debug|Win32'">
                        <PreLinkEventUseInBuild>false</PreLinkEventUseInBuild>                      
                    </PropertyGroup>
                    """)
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains("""
                add_custom_command(TARGET Project PRE_LINK
                    COMMAND "$<$<CONFIG:Release>:cmd /C echo Linking ${PROJECT_NAME}>"
                )
                """, cmake);
        }

        [Fact]
        public void Given_PostBuildEventWithConfigSpecificPostBuildEventUseInBuild_When_Converted_Then_PostBuildEventUseInBuildIsRespected()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithRawXml("""
                    <ItemDefinitionGroup>
                        <PostBuildEvent>
                            <Command>echo Completed $(ProjectName)</Command>
                        </PostBuildEvent>
                    </ItemDefinitionGroup>
                    <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Debug|Win32'">
                        <PostBuildEventUseInBuild>false</PostBuildEventUseInBuild>                      
                    </PropertyGroup>
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
                    COMMAND "$<$<CONFIG:Release>:cmd /C echo Completed ${PROJECT_NAME}>"
                )
                """, cmake);
        }
    }
}
