using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

public partial class ConverterTests
{
    public class IncludeHeadersTests
    {
        [Fact]
        public void Given_ProjectWithSourcesAndHeaders_When_IncludeHeadersIsFalse_Then_TargetSourcesDoesNotListHeaders()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithItems("ClCompile", @"src\main.cpp")
                .WithItems("ClInclude", @"include\foo.h")
                .Build()));

            var converter = new Converter(fileSystem, new InMemoryLogger());
            converter.Convert(projectFiles: [new FileInfo(@"Project.vcxproj")], includeHeaders: false);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("""                
                target_sources(Project
                    PRIVATE
                        src/main.cpp
                )
                """, cmake);
            Assert.DoesNotContain("include/foo.h", cmake);
        }

        [Fact]
        public void Given_ProjectWithSourcesAndHeaders_When_IncludeHeadersIsTrue_Then_TargetSourcesListsHeaders()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithItems("ClCompile", @"src\main.cpp")
                .WithItems("ClInclude", @"include\foo.h")
                .Build()));

            var converter = new Converter(fileSystem, new InMemoryLogger());
            converter.Convert(projectFiles: [new FileInfo(@"Project.vcxproj")], includeHeaders: true);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("""                
                target_sources(Project
                    PRIVATE
                        include/foo.h
                        src/main.cpp
                )
                """, cmake);
        }

        [Fact]
        public void Given_ProjectWithQtMocHeaders_When_IncludeHeadersIsTrue_Then_TargetSourcesListsQtMocHeaders()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithItems("ClCompile", @"src\main.cpp")
                .WithItems("QtMoc", @"include\moc.H")
                .WithItems("QtMoc", @"include\moc.HpP")
                .WithItems("QtMoc", @"include\moc.HxX")
                .WithItems("QtMoc", @"include\moc.H++")
                .WithItems("QtMoc", @"include\moc.Hh")
                .Build()));

            var converter = new Converter(fileSystem, new InMemoryLogger());
            converter.Convert(projectFiles: [new FileInfo(@"Project.vcxproj")], includeHeaders: true);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("""                
                target_sources(Project
                    PRIVATE
                        include/moc.H
                        include/moc.H++
                        include/moc.Hh
                        include/moc.HpP
                        include/moc.HxX
                        src/main.cpp
                )
                """, cmake);
        }

        [Fact]
        public void Given_ProjectWithQtMocSourceFiles_When_IncludeHeadersIsTrue_Then_TargetSourcesDoesNotListQtMocSources()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.SetCurrentDirectory(Environment.CurrentDirectory);

            fileSystem.AddFile(@"Project.vcxproj", new(TestData.Project()
                .WithItems("ClCompile", @"src\main.cpp")
                .WithItems("QtMoc", @"src\moc.cpp")
                .Build()));

            var converter = new Converter(fileSystem, new InMemoryLogger());
            converter.Convert(projectFiles: [new FileInfo(@"Project.vcxproj")], includeHeaders: true);

            var cmake = fileSystem.GetFile(@"CMakeLists.txt").TextContents;
            Assert.Contains("""                
                target_sources(Project
                    PRIVATE
                        src/main.cpp
                )
                """, cmake);
        }
    }
}

