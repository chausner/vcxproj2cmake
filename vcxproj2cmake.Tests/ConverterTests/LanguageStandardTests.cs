using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class LanguageStandardTests
    {
        [Theory]
        [InlineData("stdcpplatest", "cxx_std_23")]
        [InlineData("stdcpp23", "cxx_std_23")]
        [InlineData("stdcpp20", "cxx_std_20")]
        [InlineData("stdcpp17", "cxx_std_17")]
        [InlineData("stdcpp14", "cxx_std_14")]
        [InlineData("stdcpp11", "cxx_std_11")]
        [InlineData("Default", null)]
        public void Given_ProjectWithCppStandard_When_Converted_Then_FeaturesMatch(string standard, string? expected)
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithClCompileSetting("LanguageStandard", standard)
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            if (expected != null)
                Assert.Contains($"""
                    target_compile_features(Project
                        PRIVATE
                            {expected}
                    )
                    """, cmake);
            else
                Assert.DoesNotContain("target_compile_features", cmake);
        }

        [Theory]
        [InlineData("stdclatest", "c_std_23")]
        [InlineData("stdc23", "c_std_23")]
        [InlineData("stdc17", "c_std_17")]
        [InlineData("stdc11", "c_std_11")]
        [InlineData("Default", null)]
        public void Given_ProjectWithCStandard_When_Converted_Then_FeaturesMatch(string standard, string? expected)
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithClCompileSetting("LanguageStandard_C", standard)
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            if (expected != null)
                Assert.Contains($"""
                    target_compile_features(Project
                        PRIVATE
                            {expected}
                    )
                    """, cmake);
            else
                Assert.DoesNotContain("target_compile_features", cmake);
        }

        [Fact]
        public void Given_ProjectWithCpp17AndC11_When_Converted_Then_TargetCompileFeaturesContainsBoth()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithClCompileSetting("LanguageStandard", "stdcpp17")
                .WithClCompileSetting("LanguageStandard_C", "stdc11")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("""
                target_compile_features(Project
                    PRIVATE
                        cxx_std_17
                        c_std_11
                )
                """, cmake);
        }

        [Fact]
        public void Given_ProjectWithDefaultStandards_When_Converted_Then_NoTargetCompileFeaturesGenerated()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project().Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.DoesNotContain("target_compile_features", cmake);
        }

        [Fact]
        public void Given_InconsistentCppStandards_When_Converted_Then_Throws()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithClCompileSetting("LanguageStandard", debugValue: "stdcpp20", releaseValue: "stdcpp17")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            var ex = Assert.Throws<CatastrophicFailureException>(() =>
                converter.Convert(
                    projectFiles: [new(@"Project.vcxproj")]));

            Assert.Contains("LanguageStandard property is inconsistent between configurations", ex.Message);
        }

        [Fact]
        public void Given_InconsistentCStandards_When_Converted_Then_Throws()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithClCompileSetting("LanguageStandard_C", debugValue: "stdc17", releaseValue: "stdc11")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            var ex = Assert.Throws<CatastrophicFailureException>(() =>
                converter.Convert(
                    projectFiles: [new(@"Project.vcxproj")]));

            Assert.Contains("LanguageStandard_C property is inconsistent between configurations", ex.Message);
        }

        [Fact]
        public void Given_UnsupportedCppStandard_When_Converted_Then_Throws()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithClCompileSetting("LanguageStandard", "foo")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            var ex = Assert.Throws<CatastrophicFailureException>(() =>
                converter.Convert(
                    projectFiles: [new(@"Project.vcxproj")]));

            Assert.Contains("Unsupported C++ language standard", ex.Message);
        }

        [Fact]
        public void Given_UnsupportedCStandard_When_Converted_Then_Throws()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithClCompileSetting("LanguageStandard_C", "foo")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            var ex = Assert.Throws<CatastrophicFailureException>(() =>
                converter.Convert(
                    projectFiles: [new(@"Project.vcxproj")]));

            Assert.Contains("Unsupported C language standard", ex.Message);
        }
    }
}
