using Microsoft.Extensions.Logging;
using Xunit;

namespace vcxproj2cmake.Tests;

public class ConverterTests
{
    [Fact]
    public void Converts_simple_project()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../..", "TestData", "Simple", "test.vcxproj"));

        using var loggerFactory = LoggerFactory.Create(builder => { });
        var logger = loggerFactory.CreateLogger("test");

        var writer = new InMemoryFileWriter();
        var converter = new Converter(logger);

        converter.Convert([new(projectPath)], null, null, false, "spaces", 4, writer);

        var cmakePath = Path.Combine(Path.GetDirectoryName(projectPath)!, "CMakeLists.txt");
        var expected = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../..", "TestData", "Simple", "expected_CMakeLists.txt"));
        Assert.Equal(expected.Replace("\r\n", "\n").TrimEnd(), writer.Files[cmakePath].Replace("\r\n", "\n").TrimEnd());
    }

    [Fact]
    public void Conversion_does_not_create_files_when_using_inmemory_writer()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../..", "TestData", "Simple", "test.vcxproj"));

        using var loggerFactory = LoggerFactory.Create(builder => { });
        var logger = loggerFactory.CreateLogger("test");

        var writer = new InMemoryFileWriter();
        var converter = new Converter(logger);

        converter.Convert([new(projectPath)], null, null, false, "spaces", 4, writer);

        var cmakePath = Path.Combine(Path.GetDirectoryName(projectPath)!, "CMakeLists.txt");
        Assert.False(File.Exists(cmakePath));
    }
}
