using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class LinkerLibraryDirectoriesTests
    {
        [Fact]
        public void Given_LinkerPathsSameForAllConfigs_When_Converted_Then_TargetLinkDirectoriesAdded()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithLinkSetting("AdditionalLibraryDirectories", debugValue: "C:\\Lib\\", releaseValue: "C:\\Lib\\")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains("""
                target_link_directories(Project
                    PRIVATE
                        C:/Lib
                )
                """.Trim(), cmake);
        }

        [Fact]
        public void Given_LinkerPathsDifferentPerConfig_When_Converted_Then_GeneratorExpressionsUsed()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithLinkSetting("AdditionalLibraryDirectories", debugValue: "DebugLibs", releaseValue: "ReleaseLibs")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains("""
                target_link_directories(Project
                    PRIVATE
                        $<$<CONFIG:Debug>:DebugLibs>
                        $<$<CONFIG:Release>:ReleaseLibs>
                )
                """.Trim(), cmake);
        }

        [Fact]
        public void Given_LinkerPathsWithMSBuildMacros_When_Converted_Then_MacrosAreTranslated()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithLinkSetting("AdditionalLibraryDirectories", "$(ProjectDir)libs;$(Configuration)")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains("""
                target_link_directories(Project
                    PRIVATE
                        "${CMAKE_CURRENT_SOURCE_DIR}/libs"
                        "${CMAKE_BUILD_TYPE}"
                )
                """.Trim(), cmake);
        }

        [Fact]
        public void Given_ProjectWithAdditionalLibraryDirectoriesAndLibraryPath_When_Converted_Then_MergedPathsAreWritten()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithProperty("Debug", "Win32", "LibraryPath", "debuglib;$(LibraryPath)")
                .WithProperty("Release", "Win32", "LibraryPath", "releaselib;$(LibraryPath)")
                .WithLinkSetting("AdditionalLibraryDirectories", debugValue: "shared;additionaldebug;%(AdditionalLibraryDirectories)", releaseValue: "shared;%(AdditionalLibraryDirectories)")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains("""
                target_link_directories(Project
                    PRIVATE
                        shared
                        $<$<CONFIG:Debug>:additionaldebug>
                        $<$<CONFIG:Debug>:debuglib>
                        $<$<CONFIG:Release>:releaselib>
                )
                """, cmake);
        }
    }
}
