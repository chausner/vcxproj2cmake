using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class RemoveObsoleteIncludePathsTests
    {
        [Fact]
        public void Given_ProjectHasIncludePathAlreadySpecifiedByReferencedProjectPublicIncludes_When_Converted_Then_IncludePathIsRemovedAndLogged()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Lib/Lib.vcxproj", new(TestData.Project()
                .WithProperty("ConfigurationType", "StaticLibrary")
                .WithProperty("PublicIncludeDirectories", "include")
                .Build()));
            fileSystem.AddFile(@"App/App.vcxproj", new(TestData.Project()
                .WithProjectReferences(@"..\Lib\Lib.vcxproj")
                .WithClCompileSetting("AdditionalIncludeDirectories", @"..\Lib\include;appinclude")
                .Build()));

            var logger = new InMemoryLogger();
            var converter = new Converter(fileSystem, logger);

            // Act
            converter.Convert(
                projectFiles: [new(@"App/App.vcxproj"), new(@"Lib/Lib.vcxproj")]);

            // Assert
            Assert.FileHasContent(@"App/CMakeLists.txt", fileSystem, """
                cmake_minimum_required(VERSION 4.0)
                project(App)

                add_executable(App)

                target_include_directories(App
                    PRIVATE
                        "${CMAKE_CURRENT_SOURCE_DIR}/appinclude"
                )

                target_link_libraries(App
                    PRIVATE
                        Lib
                )
                """);

            Assert.Contains(
                "Removed include path ../Lib/include from project App since referenced project Lib specifies it as a public include directory.",
                logger.AllMessageText);
        }

        [Fact]
        public void Given_ProjectHasIncludePathAlreadySpecifiedByReferencedProjectPrivateIncludes_When_Converted_Then_IncludePathIsPreserved()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Lib/Lib.vcxproj", new(TestData.Project()
                .WithProperty("ConfigurationType", "StaticLibrary")
                .WithClCompileSetting("AdditionalIncludeDirectories", "include")
                .Build()));
            fileSystem.AddFile(@"App/App.vcxproj", new(TestData.Project()
                .WithProjectReferences(@"..\Lib\Lib.vcxproj")
                .WithClCompileSetting("AdditionalIncludeDirectories", @"..\Lib\include;appinclude")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"App/App.vcxproj"), new(@"Lib/Lib.vcxproj")]);

            // Assert
            Assert.FileHasContent(@"App/CMakeLists.txt", fileSystem, """
                cmake_minimum_required(VERSION 4.0)
                project(App)

                add_executable(App)

                target_include_directories(App
                    PRIVATE
                        "${CMAKE_CURRENT_SOURCE_DIR}/../Lib/include"
                        "${CMAKE_CURRENT_SOURCE_DIR}/appinclude"
                )

                target_link_libraries(App
                    PRIVATE
                        Lib
                )
                """);
        }

        [Fact]
        public void Given_ProjectHasIncludePathAlreadySpecifiedByTransitiveReferencedProject_When_Converted_Then_IncludePathIsRemovedAndLogged()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Core/Core.vcxproj", new(TestData.Project()
                .WithProperty("ConfigurationType", "StaticLibrary")
                .WithProperty("PublicIncludeDirectories", "include")
                .Build()));
            fileSystem.AddFile(@"Lib/Lib.vcxproj", new(TestData.Project()
                .WithProperty("ConfigurationType", "StaticLibrary")
                .WithProjectReferences(@"..\Core\Core.vcxproj")
                .Build()));
            fileSystem.AddFile(@"App/App.vcxproj", new(TestData.Project()
                .WithProjectReferences(@"..\Lib\Lib.vcxproj")
                .WithClCompileSetting("AdditionalIncludeDirectories", @"..\Core\include;appinclude")
                .Build()));

            var logger = new InMemoryLogger();
            var converter = new Converter(fileSystem, logger);

            // Act
            converter.Convert(
                projectFiles: [new(@"App/App.vcxproj"), new(@"Lib/Lib.vcxproj"), new(@"Core/Core.vcxproj")]);

            // Assert
            Assert.FileHasContent(@"App/CMakeLists.txt", fileSystem, """
                cmake_minimum_required(VERSION 4.0)
                project(App)

                add_executable(App)

                target_include_directories(App
                    PRIVATE
                        "${CMAKE_CURRENT_SOURCE_DIR}/appinclude"
                )

                target_link_libraries(App
                    PRIVATE
                        Lib
                )
                """);

            Assert.Contains(
                "Removed include path ../Core/include from project App since referenced project Core specifies it as a public include directory.",
                logger.AllMessageText);
        }

        [Fact]
        public void Given_LinkLibraryDependenciesDisabled_When_Converted_Then_IncludePathSpecifiedByReferencedProjectIsRemoved()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Lib/Lib.vcxproj", new(TestData.Project()
                .WithProperty("ConfigurationType", "StaticLibrary")
                .WithProperty("PublicIncludeDirectories", "include")
                .Build()));
            fileSystem.AddFile(@"App/App.vcxproj", new(TestData.Project()
                .WithProjectReferences(@"..\Lib\Lib.vcxproj")
                .WithItemDefinitionSetting("ProjectReference", "LinkLibraryDependencies", "false")
                .WithClCompileSetting("AdditionalIncludeDirectories", @"..\Lib\include;appinclude")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"App/App.vcxproj"), new(@"Lib/Lib.vcxproj")]);

            // Assert
            Assert.FileHasContent(@"App/CMakeLists.txt", fileSystem, """
                cmake_minimum_required(VERSION 4.0)
                project(App)

                add_executable(App)

                target_include_directories(App
                    PRIVATE
                        "${CMAKE_CURRENT_SOURCE_DIR}/appinclude"
                )

                target_link_libraries(App
                    PRIVATE
                        Lib
                )
                """);
        }

        [Fact]
        public void Given_ProjectHasIncludePathAlreadySpecifiedByReferencedInterfaceLibrary_When_Converted_Then_IncludePathIsRemoved()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"HeaderOnly/HeaderOnly.vcxproj", new(TestData.Project()
                .WithProperty("ConfigurationType", "StaticLibrary")
                .WithProperty("PublicIncludeDirectories", "include")
                .WithItems("ClInclude", "include/header.hpp")
                .Build()));
            fileSystem.AddFile(@"App/App.vcxproj", new(TestData.Project()
                .WithProjectReferences(@"..\HeaderOnly\HeaderOnly.vcxproj")
                .WithClCompileSetting("AdditionalIncludeDirectories", @"..\HeaderOnly\include;appinclude")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"App/App.vcxproj"), new(@"HeaderOnly/HeaderOnly.vcxproj")]);

            // Assert
            Assert.FileHasContent(@"App/CMakeLists.txt", fileSystem, """
                cmake_minimum_required(VERSION 4.0)
                project(App)

                add_executable(App)

                target_include_directories(App
                    PRIVATE
                        "${CMAKE_CURRENT_SOURCE_DIR}/appinclude"
                )

                target_link_libraries(App
                    PRIVATE
                        HeaderOnly
                )
                """);
        }

        [Fact]
        public void Given_ProjectsUseCurrentSourceDirMacroForDifferentDirectories_When_Converted_Then_IncludePathIsPreserved()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Lib/Lib.vcxproj", new(TestData.Project()
                .WithProperty("ConfigurationType", "StaticLibrary")
                .WithProperty("PublicIncludeDirectories", "$(ProjectDir)include")
                .Build()));
            fileSystem.AddFile(@"App/App.vcxproj", new(TestData.Project()
                .WithProjectReferences(@"..\Lib\Lib.vcxproj")
                .WithClCompileSetting("AdditionalIncludeDirectories", "$(ProjectDir)include;appinclude")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"App/App.vcxproj"), new(@"Lib/Lib.vcxproj")]);

            // Assert
            Assert.FileHasContent(@"App/CMakeLists.txt", fileSystem, """
                cmake_minimum_required(VERSION 4.0)
                project(App)

                add_executable(App)

                target_include_directories(App
                    PRIVATE
                        "${CMAKE_CURRENT_SOURCE_DIR}/include"
                        "${CMAKE_CURRENT_SOURCE_DIR}/appinclude"
                )

                target_link_libraries(App
                    PRIVATE
                        Lib
                )
                """);
        }

        [Fact]
        public void Given_ProjectsUseUnknownCMakeVariableForIncludePath_When_Converted_Then_IncludePathIsPreserved()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Lib/Lib.vcxproj", new(TestData.Project()
                .WithProperty("ConfigurationType", "StaticLibrary")
                .WithProperty("PublicIncludeDirectories", "$(ThirdParty)include")
                .Build()));
            fileSystem.AddFile(@"App/App.vcxproj", new(TestData.Project()
                .WithProjectReferences(@"..\Lib\Lib.vcxproj")
                .WithClCompileSetting("AdditionalIncludeDirectories", "$(ThirdParty)include;appinclude")
                .Build()));

            var converter = new Converter(fileSystem, NullLogger.Instance);

            // Act
            converter.Convert(
                projectFiles: [new(@"App/App.vcxproj"), new(@"Lib/Lib.vcxproj")]);

            // Assert
            Assert.FileHasContent(@"App/CMakeLists.txt", fileSystem, """
                cmake_minimum_required(VERSION 4.0)
                project(App)

                add_executable(App)

                target_include_directories(App
                    PRIVATE
                        "${ThirdParty}include"
                        "${CMAKE_CURRENT_SOURCE_DIR}/appinclude"
                )

                target_link_libraries(App
                    PRIVATE
                        Lib
                )
                """);
        }
    }
}
