using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class CompilerOptionsTests
    {
        [Fact]
        public void Given_AdditionalOptions_When_Converted_Then_OptionsAreWritten()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithClCompileSetting("AdditionalOptions", "foo bar")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains("""
                target_compile_options(Project
                    PRIVATE
                        foo
                        bar
                )
                """.TrimEnd(), cmake);
        }

        [Fact]
        public void Given_AdditionalOptionsAndPortableMode_When_Converted_Then_OptionsAreGuarded()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithClCompileSetting("AdditionalOptions", "foo bar")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")],
                portable: true);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains("""
                target_compile_options(Project
                    PRIVATE
                        "$<$<CXX_COMPILER_ID:MSVC>:foo>"
                        "$<$<CXX_COMPILER_ID:MSVC>:bar>"
                )
                """.TrimEnd(), cmake);
        }

        [Fact]
        public void Given_DisableSpecificWarnings_When_Converted_Then_WdOptionsAreWritten()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithClCompileSetting("DisableSpecificWarnings", "4100;4200")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains("""
                target_compile_options(Project
                    PRIVATE
                        /wd4100
                        /wd4200
                )
                """.TrimEnd(), cmake);
        }

        [Fact]
        public void Given_DisableSpecificWarningsAndPortableMode_When_Converted_Then_WdOptionsAreGuarded()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithClCompileSetting("DisableSpecificWarnings", "4100;4200")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")],
                portable: true);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains("""
                target_compile_options(Project
                    PRIVATE
                        "$<$<CXX_COMPILER_ID:MSVC>:/wd4100>"
                        "$<$<CXX_COMPILER_ID:MSVC>:/wd4200>"
                )
                """.TrimEnd(), cmake);
        }

        [Fact]
        public void Given_TreatSpecificWarningsAsErrors_When_Converted_Then_WeOptionsAreWritten()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithClCompileSetting("TreatSpecificWarningsAsErrors", "4800;4801")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains("""
                target_compile_options(Project
                    PRIVATE
                        /we4800
                        /we4801
                )
                """.TrimEnd(), cmake);
        }

        [Fact]
        public void Given_TreatSpecificWarningsAsErrorsAndPortableMode_When_Converted_Then_WeOptionsAreGuarded()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithClCompileSetting("TreatSpecificWarningsAsErrors", "4800;4801")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")],
                portable: true);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains("""
                target_compile_options(Project
                    PRIVATE
                        "$<$<CXX_COMPILER_ID:MSVC>:/we4800>"
                        "$<$<CXX_COMPILER_ID:MSVC>:/we4801>"
                )
                """.TrimEnd(), cmake);
        }

        [Fact]
        public void Given_WarningLevel_When_Converted_Then_WOptionIsWritten()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithClCompileSetting("WarningLevel", "Level4")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains("""
                target_compile_options(Project
                    PRIVATE
                        /W4
                )
                """.TrimEnd(), cmake);
        }

        [Fact]
        public void Given_WarningLevelAndPortableMode_When_Converted_Then_WOptionIsGuarded()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithClCompileSetting("WarningLevel", "Level4")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")],
                portable: true);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains("""
                target_compile_options(Project
                    PRIVATE
                        "$<$<CXX_COMPILER_ID:MSVC>:/W4>"
                )
                """.TrimEnd(), cmake);
        }

        [Fact]
        public void Given_InvalidWarningLevel_When_Converted_Then_Throws()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithClCompileSetting("WarningLevel", "Bad")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act & Assert
            Assert.Throws<CatastrophicFailureException>(() => converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]));
        }

        [Fact]
        public void Given_ExternalWarningLevel_When_Converted_Then_ExternalWOptionIsWritten()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithClCompileSetting("ExternalWarningLevel", "Level2")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains("""
                target_compile_options(Project
                    PRIVATE
                        /external:W2
                )
                """.TrimEnd(), cmake);
        }

        [Fact]
        public void Given_ExternalWarningLevelAndPortableMode_When_Converted_Then_ExternalWOptionIsGuarded()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithClCompileSetting("ExternalWarningLevel", "Level2")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")],
                portable: true);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains("""
                target_compile_options(Project
                    PRIVATE
                        "$<$<CXX_COMPILER_ID:MSVC>:/external:W2>"
                )
                """.TrimEnd(), cmake);
        }

        [Fact]
        public void Given_InvalidExternalWarningLevel_When_Converted_Then_Throws()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithClCompileSetting("ExternalWarningLevel", "Foo")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act & Assert
            Assert.Throws<CatastrophicFailureException>(() => converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]));
        }

        [Fact]
        public void Given_TreatAngleIncludeAsExternal_When_Converted_Then_AngleBracketsOptionIsWritten()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithClCompileSetting("TreatAngleIncludeAsExternal", "true")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains("""
                target_compile_options(Project
                    PRIVATE
                        /external:anglebrackets
                )
                """.TrimEnd(), cmake);
        }

        [Fact]
        public void Given_TreatAngleIncludeAsExternalAndPortableMode_When_Converted_Then_AngleBracketsOptionIsGuarded()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithClCompileSetting("TreatAngleIncludeAsExternal", "true")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")],
                portable: true);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains("""
                target_compile_options(Project
                    PRIVATE
                        "$<$<CXX_COMPILER_ID:MSVC>:/external:anglebrackets>"
                )
                """.TrimEnd(), cmake);
        }
    }
}
