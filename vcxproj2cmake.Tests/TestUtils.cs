using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace vcxproj2cmake.Tests;

internal static class AssertEx
{
    public static void FileHasContent(string path, MockFileSystem fileSystem, string content)
    {
        Assert.Equal(content.Trim(), fileSystem.GetFile(path).TextContents.Trim());
    }
}
