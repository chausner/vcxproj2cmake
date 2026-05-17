using Xunit;

namespace vcxproj2cmake.Tests;

public class TextWriterExtensionsTests
{
    const string ResetForeground = "\e[39m\e[22m";
    const string ResetBackground = "\e[49m";

    [Fact]
    public void When_WriteColoredCalledWithNoColors_Then_WritesMessageOnly()
    {
        using var writer = new StringWriter();
        var message = "Hello";

        writer.WriteColored(message, background: null, foreground: null);

        Assert.Equal(message, writer.ToString());
    }

    [Theory]
    [InlineData(ConsoleColor.DarkGreen, "\e[32m")] // regular
    [InlineData(ConsoleColor.Red, "\e[1m\e[31m")] // bold + bright
    [InlineData(ConsoleColor.Gray, "\e[37m")] // regular gray
    public void When_WriteColoredCalledWithForegroundOnly_Then_WritesFgCodesAndResets(ConsoleColor fg, string expectedFgCode)
    {
        using var writer = new StringWriter();
        var message = "Msg";

        writer.WriteColored(message, background: null, foreground: fg);

        var expected = expectedFgCode + message + ResetForeground;
        Assert.Equal(expected, writer.ToString());
    }

    [Theory]
    [InlineData(ConsoleColor.DarkBlue, "\e[44m")]
    [InlineData(ConsoleColor.Gray, "\e[47m")]
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

        // Background Gray -> \e[47m, Foreground White -> \e[1m\e[37m
        writer.WriteColored(message, background: ConsoleColor.Gray, foreground: ConsoleColor.White);

        var expected = "\e[47m" + "\e[1m\e[37m" + message + ResetForeground + ResetBackground;
        Assert.Equal(expected, writer.ToString());
    }

    [Fact]
    public void When_WriteLineColoredCalledWithColors_Then_AppendsNewLineAndResets()
    {
        using var writer = new StringWriter();
        var message = "Line";

        // Background DarkCyan -> \e[46m, Foreground Yellow -> \e[1m\e[33m
        writer.WriteLineColored(message, background: ConsoleColor.DarkCyan, foreground: ConsoleColor.Yellow);

        var expected = "\e[46m" + "\e[1m\e[33m" + message + Environment.NewLine + ResetForeground + ResetBackground;
        Assert.Equal(expected, writer.ToString());
    }
}

