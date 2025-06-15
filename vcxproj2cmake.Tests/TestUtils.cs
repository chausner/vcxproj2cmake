using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

internal static class AssertEx
{
    public static void FileHasContent(string path, MockFileSystem fileSystem, string content)
    {
        var trimmedExpectedContent = content.Trim();
        var trimmedContent = fileSystem.GetFile(path).TextContents.Trim();
        Assert.Equal(trimmedExpectedContent, trimmedContent);
    }
}

internal class InMemoryLogger : ILogger
{
    public ConcurrentQueue<string> Messages { get; } = new();
    public IDisposable BeginScope<TState>(TState state) => null!;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Messages.Enqueue(formatter(state, exception));
    }
}