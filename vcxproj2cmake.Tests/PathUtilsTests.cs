using Xunit;

namespace vcxproj2cmake.Tests;

public class PathUtilsTests
{
    // Test data taken from https://learn.microsoft.com/en-us/cpp/c-language/parsing-c-command-line-arguments.
    [Theory]
    [InlineData("""
        "a b c" d e
        """, new[] { "a b c", "d", "e" })]
    [InlineData("""
        "ab\"c" "\\" d
        """, new[] { """ab"c""", """\""", """d""" })]
    [InlineData("""
        a\\\b d"e f"g h
        """, new[] { """a\\\b""", """de fg""", """h""" })]
    [InlineData("""
        a\\\"b c d
        """, new[] { """a\"b""", """c""", """d""" })]
    [InlineData("""
        a\\\\"b c" d e
        """, new[] { """a\\b c""", """d""", """e""" })]
    [InlineData("""
        a"b"" c d
        """, new[] { """ab" c d""" })]
    public void SplitArguments_ReturnsCorrectlyParsedArguments(string arguments, string[] expectedArgs)
    {
        // Act
        string[] args = PathUtils.SplitArguments(arguments);

        // Assert
        Assert.Equal(expectedArgs, args);
    }
}
