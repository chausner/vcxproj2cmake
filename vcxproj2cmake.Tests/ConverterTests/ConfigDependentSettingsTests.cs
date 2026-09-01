using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Runtime.InteropServices;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class ConfigDependentSettingsTests
    {
        [Fact]
        public void Given_LinkerPathsWithoutCondition_When_Converted_Then_TargetLinkDirectoriesAdded()
        {
            Assert.SkipUnless(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "Absolute Windows paths are currently broken on non-Windows platforms");

            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithLinkSetting("AdditionalLibraryDirectories", "C:/Lib")
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
        public void Given_LinkerPathsSameForAllConfigs_When_Converted_Then_TargetLinkDirectoriesAdded()
        {
            Assert.SkipUnless(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "Absolute Windows paths are currently broken on non-Windows platforms");

            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithLinkSetting("AdditionalLibraryDirectories", debugValue: "C:/Lib/", releaseValue: "C:/Lib/")
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
        public void Given_LinkerPathsWithUnsupportedConfigSpecificValues_When_Converted_Then_UnsupportedConfigurationIgnored()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithConfigurations(("Debug", "Win32"), ("Release", "Win32"), ("MinSizeRel", "Win32"))
                .WithItemDefinitionSetting("Debug", "Win32", "Link", "AdditionalLibraryDirectories", "SupportedLib")
                .WithItemDefinitionSetting("Release", "Win32", "Link", "AdditionalLibraryDirectories", "SupportedLib")
                .WithItemDefinitionSetting("MinSizeRel", "Win32", "Link", "AdditionalLibraryDirectories", "UnsupportedLib")
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
                        "${CMAKE_CURRENT_SOURCE_DIR}/SupportedLib"
                )
                """, cmake);
            Assert.DoesNotContain("$<$<CONFIG:Debug>:", cmake);
            Assert.DoesNotContain("$<$<CONFIG:Release>:", cmake);
            Assert.Contains("Skipping unsupported project configuration: MinSizeRel|Win32", logger.AllMessageText);
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
        public void Given_LinkerPathsDifferentPerPlatform_When_Converted_Then_GeneratorExpressionsUseArchitecture()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithConfigurations(("Debug", "Win32"), ("Debug", "x64"))
                .WithItemDefinitionSetting("Debug", "Win32", "Link", "AdditionalLibraryDirectories", "Win32Lib")
                .WithItemDefinitionSetting("Debug", "x64", "Link", "AdditionalLibraryDirectories", "X64Lib")
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
                        "$<$<STREQUAL:${CMAKE_CXX_COMPILER_ARCHITECTURE_ID},X86>:${CMAKE_CURRENT_SOURCE_DIR}/Win32Lib>"
                        "$<$<STREQUAL:${CMAKE_CXX_COMPILER_ARCHITECTURE_ID},x64>:${CMAKE_CURRENT_SOURCE_DIR}/X64Lib>"
                )
                """, cmake);
        }

        [Fact]
        public void Given_LinkerPathsDifferentPerConfigAndPlatform_When_Converted_Then_SkippedWithWarning()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithConfigurations(("Debug", "Win32"), ("Debug", "x64"), ("Release", "Win32"), ("Release", "x64"))
                .WithItemDefinitionSetting("Debug", "Win32", "Link", "AdditionalLibraryDirectories", "DebugWin32")
                .WithItemDefinitionSetting("Debug", "x64", "Link", "AdditionalLibraryDirectories", "DebugX64")
                .WithItemDefinitionSetting("Release", "Win32", "Link", "AdditionalLibraryDirectories", "ReleaseWin32")
                .WithItemDefinitionSetting("Release", "x64", "Link", "AdditionalLibraryDirectories", "ReleaseX64")
                .Build()));

            var logger = new InMemoryLogger();
            var converter = new Converter(fileSystem, logger);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.DoesNotContain("target_link_directories(Project", cmake);
            Assert.Contains("ignored because they are specific to certain build configurations", logger.AllMessageText);
        }

        [Fact]
        public void Given_DefaultConfiguration_When_LinkerPathsDifferPerConfigAndPlatform_Then_DefaultConfigurationValueUsedForAllConfigurations()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithConfigurations(("Debug", "Win32"), ("Debug", "x64"), ("Release", "Win32"), ("Release", "x64"))
                .WithItemDefinitionSetting("Debug", "Win32", "Link", "AdditionalLibraryDirectories", "DebugWin32")
                .WithItemDefinitionSetting("Debug", "x64", "Link", "AdditionalLibraryDirectories", "DebugX64")
                .WithItemDefinitionSetting("Release", "Win32", "Link", "AdditionalLibraryDirectories", "ReleaseWin32")
                .WithItemDefinitionSetting("Release", "x64", "Link", "AdditionalLibraryDirectories", "ReleaseX64")
                .Build()));

            var logger = new InMemoryLogger();
            var converter = new Converter(fileSystem, logger);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")],
                defaultConfiguration: "Debug|x64");

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains("""
                target_link_directories(Project
                    PRIVATE
                        "${CMAKE_CURRENT_SOURCE_DIR}/DebugX64"
                )
                """, cmake);

            Assert.DoesNotContain("ignored because they are specific to certain build configurations", logger.AllMessageText);
        }

        [Fact]
        public void Given_InvalidDefaultConfiguration_When_Converted_Then_ThrowsWithValidConfigurations()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project().Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            var exception = Assert.Throws<CatastrophicFailureException>(() => converter.Convert(
                projectFiles: [new(@"Project.vcxproj")],
                defaultConfiguration: "Invalid|Win32"));

            // Assert
            Assert.Equal(
                "Default project configuration 'Invalid|Win32' is invalid. Valid configurations are: Debug|Win32, Release|Win32",
                exception.Message);
        }

        [Fact]
        public void Given_LinkerPathsOverwrittenMultipleTimes_When_Converted_Then_LastValueUsed()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithRawXml("""
                    <ItemDefinitionGroup>
                        <Link>
                            <AdditionalLibraryDirectories>Libs1</AdditionalLibraryDirectories>
                        </Link>
                    </ItemDefinitionGroup>
                    <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Debug|Win32'">
                        <Link>
                            <AdditionalLibraryDirectories>DebugLib1</AdditionalLibraryDirectories>
                        </Link>
                    </ItemDefinitionGroup>
                    <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Release|Win32'">
                        <Link>
                            <AdditionalLibraryDirectories>ReleaseLib1</AdditionalLibraryDirectories>
                        </Link>
                    </ItemDefinitionGroup>
                    <ItemDefinitionGroup>
                        <Link>
                            <AdditionalLibraryDirectories>Libs2</AdditionalLibraryDirectories>
                        </Link>
                    </ItemDefinitionGroup>
                    <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Debug|Win32'">
                        <Link>
                            <AdditionalLibraryDirectories>DebugLib2</AdditionalLibraryDirectories>
                        </Link>
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
                target_link_directories(Project
                    PRIVATE
                        "$<$<CONFIG:Debug>:${CMAKE_CURRENT_SOURCE_DIR}/DebugLib2>"
                        "$<$<CONFIG:Release>:${CMAKE_CURRENT_SOURCE_DIR}/Libs2>"
                )
                """, cmake);
            Assert.DoesNotContain("DebugLib1", cmake);
            Assert.DoesNotContain("Libs1", cmake);
            Assert.DoesNotContain("ReleaseLib1", cmake);
        }
    }
}
