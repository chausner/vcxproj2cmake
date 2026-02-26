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
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.CreateProjectWithClCompileProperty("AdditionalOptions", "foo bar", "foo bar")));

            var converter = new Converter(fileSystem, NullLogger.Instance);
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("""
                target_compile_options(Project
                    PUBLIC
                        foo
                        bar
                )
                """.TrimEnd(), cmake);
        }

        [Fact]
        public void Given_DisableSpecificWarnings_When_Converted_Then_WdOptionsAreWritten()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.CreateProjectWithClCompileProperty("DisableSpecificWarnings", "4100;4200", "4100;4200")));

            var converter = new Converter(fileSystem, NullLogger.Instance);
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("""
                target_compile_options(Project
                    PUBLIC
                        /wd4100
                        /wd4200
                )
                """.TrimEnd(), cmake);
        }

        [Fact]
        public void Given_TreatSpecificWarningsAsErrors_When_Converted_Then_WeOptionsAreWritten()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.CreateProjectWithClCompileProperty("TreatSpecificWarningsAsErrors", "4800;4801", "4800;4801")));

            var converter = new Converter(fileSystem, NullLogger.Instance);
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("""
                target_compile_options(Project
                    PUBLIC
                        /we4800
                        /we4801
                )
                """.TrimEnd(), cmake);
        }

        [Fact]
        public void Given_WarningLevel_When_Converted_Then_WOptionIsWritten()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.CreateProjectWithClCompileProperty("WarningLevel", "Level4", "Level4")));

            var converter = new Converter(fileSystem, NullLogger.Instance);
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("""
                target_compile_options(Project
                    PUBLIC
                        /W4
                )
                """.TrimEnd(), cmake);
        }

        [Fact]
        public void Given_InvalidWarningLevel_When_Converted_Then_Throws()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.CreateProjectWithClCompileProperty("WarningLevel", "Bad", "Bad")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            Assert.Throws<CatastrophicFailureException>(() => converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]));
        }

        [Fact]
        public void Given_ExternalWarningLevel_When_Converted_Then_ExternalWOptionIsWritten()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.CreateProjectWithClCompileProperty("ExternalWarningLevel", "Level2", "Level2")));

            var converter = new Converter(fileSystem, NullLogger.Instance);
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("""
                target_compile_options(Project
                    PUBLIC
                        /external:W2
                )
                """.TrimEnd(), cmake);
        }

        [Fact]
        public void Given_InvalidExternalWarningLevel_When_Converted_Then_Throws()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.CreateProjectWithClCompileProperty("ExternalWarningLevel", "Foo", "Foo")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            Assert.Throws<CatastrophicFailureException>(() => converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]));
        }

        [Fact]
        public void Given_TreatAngleIncludeAsExternal_When_Converted_Then_AngleBracketsOptionIsWritten()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.CreateProjectWithClCompileProperty("TreatAngleIncludeAsExternal", "true", "true")));

            var converter = new Converter(fileSystem, NullLogger.Instance);
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("""
                target_compile_options(Project
                    PUBLIC
                        /external:anglebrackets
                )
                """.TrimEnd(), cmake);
        }
    }
}
