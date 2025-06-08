using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace vcxproj2cmake;

public interface ICMakeFileWriter
{
    void WriteFile(string path, string content);
}

class DiskFileWriter : ICMakeFileWriter
{
    public void WriteFile(string path, string content)
    {
        File.WriteAllText(path, content);
    }
}

class ConsoleFileWriter : ICMakeFileWriter
{
    readonly ILogger logger;
    public ConsoleFileWriter(ILogger logger)
    {
        this.logger = logger;
    }
    public void WriteFile(string path, string content)
    {
        var newline = Environment.NewLine;
        var indentedContent = Regex.Replace(content, "^", "    ", RegexOptions.Multiline);
        logger.LogInformation($"Generated output for {path}:{newline}{newline}{indentedContent}");
    }
}
