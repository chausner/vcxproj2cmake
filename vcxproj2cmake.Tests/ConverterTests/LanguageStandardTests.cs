using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Scriban.Syntax;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class LanguageStandardTests
    {
        static string CreateProject(string? cppDebug = null, string? cppRelease = null, string? cDebug = null, string? cRelease = null) => $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Project DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
                <ItemGroup Label="ProjectConfigurations">
                    <ProjectConfiguration Include="Debug|Win32">
                        <Configuration>Debug</Configuration>
                        <Platform>Win32</Platform>
                    </ProjectConfiguration>
                    <ProjectConfiguration Include="Release|Win32">
                        <Configuration>Release</Configuration>
                        <Platform>Win32</Platform>
                    </ProjectConfiguration>
                </ItemGroup>
                <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Debug|Win32'">
                    <ClCompile>
                        {(cppDebug != null ? $"<LanguageStandard>{cppDebug}</LanguageStandard>" : string.Empty)}
                        {(cDebug != null ? $"<LanguageStandard_C>{cDebug}</LanguageStandard_C>" : string.Empty)}
                    </ClCompile>
                </ItemDefinitionGroup>
                <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Release|Win32'">
                    <ClCompile>
                        {(cppRelease != null ? $"<LanguageStandard>{cppRelease}</LanguageStandard>" : string.Empty)}
                        {(cRelease != null ? $"<LanguageStandard_C>{cRelease}</LanguageStandard_C>" : string.Empty)}
                    </ClCompile>
                </ItemDefinitionGroup>
            </Project>
            """;

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

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProject(standard, standard)));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            if (expected != null)
                Assert.Contains($"""
                    target_compile_features(Project
                        PUBLIC
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

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProject(null, null, standard, standard)));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            if (expected != null)
                Assert.Contains($"""
                    target_compile_features(Project
                        PUBLIC
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

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProject("stdcpp17", "stdcpp17", "stdc11", "stdc11")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("""
                target_compile_features(Project
                    PUBLIC
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

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProject()));

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

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProject("stdcpp20", "stdcpp17")));

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

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProject(null, null, "stdc17", "stdc11")));

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

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProject("foo", "foo")));

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

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProject(null, null, "foo", "foo")));    

            var converter = new Converter(fileSystem, NullLogger.Instance);

            var ex = Assert.Throws<CatastrophicFailureException>(() =>
                converter.Convert(
                    projectFiles: [new(@"Project.vcxproj")]));

            Assert.Contains("Unsupported C language standard", ex.Message);
        }        
    }
}
