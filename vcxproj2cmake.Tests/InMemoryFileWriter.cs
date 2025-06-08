namespace vcxproj2cmake.Tests;

internal class InMemoryFileWriter : ICMakeFileWriter
{
    public Dictionary<string, string> Files { get; } = new();

    public void WriteFile(string path, string content)
    {
        Files[path] = content;
    }
}
