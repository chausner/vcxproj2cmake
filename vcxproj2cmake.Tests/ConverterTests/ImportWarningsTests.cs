using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class ImportWarningsTests
    {
        [Fact]
        public void Given_ProjectWithUnexpectedImport_When_Converted_Then_LogsWarning()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);
            fileSystem.AddFile(@"Project.vcxproj", new MockFileData(TestData.Project()
                .WithImports(
                    @"$(VCTargetsPath)\Microsoft.Cpp.Default.props",
                    "custom.props",
                    @"$(VCTargetsPath)\Microsoft.Cpp.targets")
                .Build()));

            var logger = new InMemoryLogger();
            var converter = new Converter(fileSystem, logger);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")],
                dryRun: true);

            // Assert
            Assert.Contains("MSBuild imports are unsupported and will not be processed: custom.props", logger.AllMessageText);
        }

        [Fact]
        public void Given_ProjectWithOnlyStandardVisualStudioImports_When_Converted_Then_LogsNoImportWarnings()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);
            fileSystem.AddFile(@"Project.vcxproj", new MockFileData(TestData.Project()
                .WithImports(
                    @"$(VCTargetsPath)\Microsoft.Cpp.Default.props",
                    @"$(VCTargetsPath)\Microsoft.Cpp.props",
                    @"$(UserRootDir)\Microsoft.Cpp.$(Platform).user.props",
                    @"$(VCTargetsPath)\Microsoft.Cpp.targets")
                .Build()));

            var logger = new InMemoryLogger();
            var converter = new Converter(fileSystem, logger);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")],
                dryRun: true);

            // Assert
            Assert.DoesNotContain("MSBuild imports are unsupported and will not be processed", logger.AllMessageText);
        }

        [Fact]
        public void Given_ProjectWithQtMsBuildImports_When_Converted_Then_LogsNoImportWarnings()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);
            fileSystem.AddFile(@"Project.vcxproj", new MockFileData(TestData.Project()
                .WithImports(
                    @"$(QtMsBuild)\qt_defaults.props",
                    @"$(QtMsBuild)\qt.targets")
                .Build()));

            var logger = new InMemoryLogger();
            var converter = new Converter(fileSystem, logger);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")],
                dryRun: true);

            // Assert
            Assert.DoesNotContain("MSBuild imports are unsupported and will not be processed", logger.AllMessageText);
        }

        [Fact]
        public void Given_ProjectWithConanImports_When_Converted_Then_LogsNoImportWarnings()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);
            fileSystem.AddFile(@"Project.vcxproj", new MockFileData(TestData.Project()
                .WithImports(
                    @"conan_boost.props",
                    @"packages\conan_fmt.props",
                    @"build\generators\conandeps.props",
                    @"$(ConanDir)conan_fmt.props")
                .Build()));

            var logger = new InMemoryLogger();
            var converter = new Converter(fileSystem, logger);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")],
                dryRun: true);

            // Assert
            Assert.DoesNotContain("MSBuild imports are unsupported and will not be processed", logger.AllMessageText);
        }
    }
}
