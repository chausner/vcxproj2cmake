using Xunit;

namespace vcxproj2cmake.Tests;

public class TextWriterExtensionsTests
{
    private const string ResetForeground = "\x1B[39m\x1B[22m";
    private const string ResetBackground = "\x1B[49m";

    [Fact]
    public void When_WriteColoredCalledWithNoColors_Then_WritesMessageOnly()
    {
        using var writer = new StringWriter();
        var message = "Hello";

        writer.WriteColored(message, background: null, foreground: null);

        Assert.Equal(message, writer.ToString());
    }

    [Theory]
    [InlineData(ConsoleColor.DarkGreen, "\x1B[32m")] // regular
    [InlineData(ConsoleColor.Red, "\x1B[1m\x1B[31m")] // bold + bright
    [InlineData(ConsoleColor.Gray, "\x1B[37m")] // regular gray
    public void When_WriteColoredCalledWithForegroundOnly_Then_WritesFgCodesAndResets(ConsoleColor fg, string expectedFgCode)
    {
        using var writer = new StringWriter();
        var message = "Msg";

        writer.WriteColored(message, background: null, foreground: fg);

        var expected = expectedFgCode + message + ResetForeground;
        Assert.Equal(expected, writer.ToString());
    }

    [Theory]
    [InlineData(ConsoleColor.DarkBlue, "\x1B[44m")]
    [InlineData(ConsoleColor.Gray, "\x1B[47m")]
    public void When_WriteColoredCalledWithBackgroundOnly_Then_WritesBgCodesAndResets(ConsoleColor bg, string expectedBgCode)
    {
        using var writer = new StringWriter();
        var message = "Data";

        writer.WriteColored(message, background: bg, foreground: null);

        var expected = expectedBgCode + message + ResetBackground;
        Assert.Equal(expected, writer.ToString());
    }

    [Fact]
    public void When_WriteColoredCalledWithBothColors_Then_OrderIsBgThenFgMessageThenResets()
    {
        using var writer = new StringWriter();
        var message = "Both";

        // Background Gray -> \x1B[47m, Foreground White -> \x1B[1m\x1B[37m
        writer.WriteColored(message, background: ConsoleColor.Gray, foreground: ConsoleColor.White);

        var expected = "\x1B[47m" + "\x1B[1m\x1B[37m" + message + ResetForeground + ResetBackground;
        Assert.Equal(expected, writer.ToString());
    }

    [Fact]
    public void When_WriteLineColoredCalledWithColors_Then_AppendsNewLineAndResets()
    {
        using var writer = new StringWriter();
        var message = "Line";

        // Background DarkCyan -> \x1B[46m, Foreground Yellow -> \x1B[1m\x1B[33m
        writer.WriteLineColored(message, background: ConsoleColor.DarkCyan, foreground: ConsoleColor.Yellow);

        var expected = "\x1B[46m" + "\x1B[1m\x1B[33m" + message + Environment.NewLine + ResetForeground + ResetBackground;
        Assert.Equal(expected, writer.ToString());
    }
}

