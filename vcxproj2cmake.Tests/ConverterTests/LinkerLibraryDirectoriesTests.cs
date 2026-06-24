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
                """, cmake);
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
                        "$<$<CONFIG:Debug>:DebugLibs>"
                        "$<$<CONFIG:Release>:ReleaseLibs>"
                )
                """, cmake);
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
                """, cmake);
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
                        "$<$<CONFIG:Debug>:additionaldebug>"
                        "$<$<CONFIG:Debug>:debuglib>"
                        "$<$<CONFIG:Release>:releaselib>"
                )
                """, cmake);
        }

        [Fact]
        public void Given_ProjectWithDefaultLibraryPaths_When_Converted_Then_ValuesAreIgnoredAndNoWarningsAreGenerated()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithProperty("Debug", "Win32", "LibraryPath", "debuglib;$(VC_LibraryPath_x86);$(VC_LibraryPath_x64);$(VC_LibraryPath_ARM);$(VC_LibraryPath_ARM64);$(WindowsSDK_LibraryPath_x86);$(WindowsSDK_LibraryPath_x64);$(WindowsSDK_LibraryPath_ARM);$(WindowsSDK_LibraryPath_ARM64);$(NETFXKitsDir)Lib\\um\\x86;$(NETFXKitsDir)Lib\\um\\x64;$(NETFXKitsDir)Lib\\um\\arm;$(NETFXKitsDir)Lib\\um\\arm64;$(LibraryPath)")
                .WithProperty("Release", "Win32", "LibraryPath", "releaselib;$(VC_LibraryPath_x86);$(VC_LibraryPath_x64);$(VC_LibraryPath_ARM);$(VC_LibraryPath_ARM64);$(WindowsSDK_LibraryPath_x86);$(WindowsSDK_LibraryPath_x64);$(WindowsSDK_LibraryPath_ARM);$(WindowsSDK_LibraryPath_ARM64);$(NETFXKitsDir)Lib\\um\\x86;$(NETFXKitsDir)Lib\\um\\x64;$(NETFXKitsDir)Lib\\um\\arm;$(NETFXKitsDir)Lib\\um\\arm64;$(LibraryPath)")
                .Build()));

            var logger = new InMemoryLogger();
            var converter = new Converter(fileSystem, logger);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains("""
                target_link_directories(Project
                    PRIVATE
                        "$<$<CONFIG:Debug>:debuglib>"
                        "$<$<CONFIG:Release>:releaselib>"
                )
                """, cmake);

            Assert.DoesNotContain(@"VC_LibraryPath_x86", logger.AllMessageText);
            Assert.DoesNotContain(@"VC_LibraryPath_x64", logger.AllMessageText);
            Assert.DoesNotContain(@"VC_LibraryPath_ARM", logger.AllMessageText);
            Assert.DoesNotContain(@"VC_LibraryPath_ARM64", logger.AllMessageText);
            Assert.DoesNotContain(@"WindowsSDK_LibraryPath_x86", logger.AllMessageText);
            Assert.DoesNotContain(@"WindowsSDK_LibraryPath_x64", logger.AllMessageText);
            Assert.DoesNotContain(@"WindowsSDK_LibraryPath_ARM", logger.AllMessageText);
            Assert.DoesNotContain(@"WindowsSDK_LibraryPath_ARM64", logger.AllMessageText);
            Assert.DoesNotContain(@"$(NETFXKitsDir)Lib\\um\\x86", logger.AllMessageText);
            Assert.DoesNotContain(@"$(NETFXKitsDir)Lib\\um\\x64", logger.AllMessageText);
            Assert.DoesNotContain(@"$(NETFXKitsDir)Lib\\um\\arm", logger.AllMessageText);
            Assert.DoesNotContain(@"$(NETFXKitsDir)Lib\\um\\arm64", logger.AllMessageText);
        }
    }
}
