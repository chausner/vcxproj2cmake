using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Runtime.InteropServices;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class LinkerLibraryDirectoriesTests
    {
        [Fact]
        public void Given_LinkerPathsSameForAllConfigs_When_Converted_Then_TargetLinkDirectoriesAdded()
        {
            Assert.SkipUnless(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "Absolute Windows paths are currently broken on non-Windows platforms");

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
        public void Given_ProjectWithRelativeLinkerDirectories_When_Converted_Then_PathsArePrefixed()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithLinkSetting("AdditionalLibraryDirectories", "libs;..\\shared")
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
                        "${CMAKE_CURRENT_SOURCE_DIR}/../shared"
                )
                """, cmake);
        }

        [Fact]
        public void Given_ProjectWithOutputMacroLinkerDirectories_When_Converted_Then_GeneratorExpressionPathsAreNotPrefixed()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithLinkSetting("AdditionalLibraryDirectories", "$(TargetDir)generated;$(IntDir)cache")
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
                        "$<TARGET_FILE_DIR:Project>/generated"
                        "${CMAKE_CURRENT_BINARY_DIR}/cache"
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
                        "$<$<CONFIG:Debug>:${CMAKE_CURRENT_SOURCE_DIR}/DebugLibs>"
                        "$<$<CONFIG:Release>:${CMAKE_CURRENT_SOURCE_DIR}/ReleaseLibs>"
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
                        "$<CONFIG>"
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
                        "${CMAKE_CURRENT_SOURCE_DIR}/shared"
                        "$<$<CONFIG:Debug>:${CMAKE_CURRENT_SOURCE_DIR}/additionaldebug>"
                        "$<$<CONFIG:Debug>:${CMAKE_CURRENT_SOURCE_DIR}/debuglib>"
                        "$<$<CONFIG:Release>:${CMAKE_CURRENT_SOURCE_DIR}/releaselib>"
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
                        "$<$<CONFIG:Debug>:${CMAKE_CURRENT_SOURCE_DIR}/debuglib>"
                        "$<$<CONFIG:Release>:${CMAKE_CURRENT_SOURCE_DIR}/releaselib>"
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
