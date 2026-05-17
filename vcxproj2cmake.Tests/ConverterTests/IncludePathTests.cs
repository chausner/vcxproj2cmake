using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class IncludePathTests
    {
        [Fact]
        public void Given_ProjectWithIncludeDirectories_When_Converted_Then_PathsAreWritten()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithClCompileSetting("AdditionalIncludeDirectories", "$(ProjectDir)include;..\\shared")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("""
                target_include_directories(Project
                    PRIVATE
                        "${CMAKE_CURRENT_SOURCE_DIR}/include"
                        "${CMAKE_CURRENT_SOURCE_DIR}/../shared"
                )
                """, cmake);
        }

        [Fact]
        public void Given_ProjectWithConfigSpecificIncludeDirectories_When_Converted_Then_GeneratorExpressionsUsed()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithClCompileSetting("AdditionalIncludeDirectories", debugValue: "debug", releaseValue: "release")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("""
                target_include_directories(Project
                    PRIVATE
                        $<$<CONFIG:Debug>:${CMAKE_CURRENT_SOURCE_DIR}/debug>
                        $<$<CONFIG:Release>:${CMAKE_CURRENT_SOURCE_DIR}/release>
                )
                """, cmake);
        }

        [Fact]
        public void Given_ProjectWithAdditionalIncludeDirectoriesAndIncludePath_When_Converted_Then_MergedPathsAreWritten()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithProperty("Debug", "Win32", "IncludePath", "debuginc;$(IncludePath)")
                .WithProperty("Release", "Win32", "IncludePath", "releaseinc;$(IncludePath)")
                .WithClCompileSetting("AdditionalIncludeDirectories", debugValue: "shared;additionaldebug;%(AdditionalIncludeDirectories)", releaseValue: "shared;%(AdditionalIncludeDirectories)")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("""
                target_include_directories(Project
                    PRIVATE
                        "${CMAKE_CURRENT_SOURCE_DIR}/shared"
                        $<$<CONFIG:Debug>:${CMAKE_CURRENT_SOURCE_DIR}/additionaldebug>
                        $<$<CONFIG:Debug>:${CMAKE_CURRENT_SOURCE_DIR}/debuginc>
                        $<$<CONFIG:Release>:${CMAKE_CURRENT_SOURCE_DIR}/releaseinc>
                )
                """, cmake);
        }

        [Fact]
        public void Given_ProjectWithPublicIncludeDirectories_When_Converted_Then_InterfacePathsAreWritten()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithProperty("PublicIncludeDirectories", "public;..\\common")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("""
                target_include_directories(Project
                    INTERFACE
                        "${CMAKE_CURRENT_SOURCE_DIR}/public"
                        "${CMAKE_CURRENT_SOURCE_DIR}/../common"
                )
                """, cmake);
            Assert.DoesNotContain("PRIVATE\n", cmake); // only INTERFACE section expected
        }
        
        [Fact]
        public void Given_ProjectWithAdditionalIncludeDirectoriesAndPublicIncludeDirectories_When_Converted_Then_PrivateAndInterfacePathsAreWritten()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithProperty("PublicIncludeDirectories", "public")
                .WithClCompileSetting("AdditionalIncludeDirectories", debugValue: "private", releaseValue: "private")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("""
                target_include_directories(Project
                    INTERFACE
                        "${CMAKE_CURRENT_SOURCE_DIR}/public"
                    PRIVATE
                        "${CMAKE_CURRENT_SOURCE_DIR}/private"
                )
                """, cmake);
        }

        [Fact]
        public void Given_AllProjectIncludesArePublic_When_Converted_Then_ProjectDirIsAdded()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithProperty("AllProjectIncludesArePublic", "true")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("""
                target_include_directories(Project
                    INTERFACE
                        "${CMAKE_CURRENT_SOURCE_DIR}"
                )
                """, cmake);
        }

        [Fact]
        public void Given_InvalidAllProjectIncludesArePublicValue_When_Converted_Then_Throws()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithProperty("AllProjectIncludesArePublic", "foo")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            var ex = Assert.Throws<CatastrophicFailureException>(() => converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]));

            Assert.Contains("Invalid value for AllProjectIncludesArePublic", ex.Message);
        }
    }
}
