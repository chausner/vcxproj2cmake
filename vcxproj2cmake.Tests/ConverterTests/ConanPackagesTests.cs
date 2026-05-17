using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class ConanPackagesTests
    {
        static string CreateProjectWithConanImports(params string[] packages)
            => TestData.Project()
                .WithImports(packages.Select(package => $"conan_{package}.props"))
                .Build();

        [Fact]
        public void Given_ProjectWithKnownConanPackage_When_Converted_Then_FindPackageAndTargetLinkLibrariesAreGenerated()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProjectWithConanImports("boost")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains("find_package(Boost REQUIRED CONFIG)", cmake);
            Assert.Contains(
                """
                target_link_libraries(Project
                    PRIVATE
                        boost::boost
                )
                """.Trim(),
                cmake);
        }

        [Fact]
        public void Given_ProjectWithUnknownConanPackage_When_Converted_Then_DefaultNamesAreUsed()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProjectWithConanImports("unknown")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains("find_package(unknown REQUIRED CONFIG)", cmake);
            Assert.Contains(
                """
                target_link_libraries(Project
                    PRIVATE
                        unknown::unknown
                )
                """.Trim(),
                cmake);
        }

        [Fact]
        public void Given_ProjectWithMultipleConanPackages_When_Converted_Then_AllPackagesAreLinked()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(CreateProjectWithConanImports("boost", "fmt")));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"Project.vcxproj")]);

            // Assert
            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;

            Assert.Contains("find_package(Boost REQUIRED CONFIG)", cmake);
            Assert.Contains("find_package(fmt REQUIRED CONFIG)", cmake);
            Assert.Contains(
                """
                target_link_libraries(Project
                    PRIVATE
                        boost::boost
                        fmt::fmt
                )
                """.Trim(),
                cmake);
        }
    }
}
