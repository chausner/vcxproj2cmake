using System.Text;
using System.Text.RegularExpressions;

namespace vcxproj2cmake;

static class PathUtils
{
    public static string NormalizePathSeparators(string path)
    {
        return path.Replace('\\', Path.DirectorySeparatorChar)
                   .Replace('/', Path.DirectorySeparatorChar);
    }

    public static string NormalizePath(string path)
    {
        if (path == string.Empty)
            return string.Empty;

        // In CMake, we should always use forward-slashes as directory separator, even on Windows
        string normalizedPath = path.Replace(@"\", "/");

        // Remove duplicated separators
        normalizedPath = Regex.Replace(normalizedPath, @"//+", "/");

        // Remove ./ prefix(es)
        while (normalizedPath.StartsWith("./"))
            normalizedPath = normalizedPath[2..];
        if (normalizedPath == string.Empty)
            return ".";

        // Remove /. suffix(es)
        while (normalizedPath.EndsWith("/."))
            normalizedPath = normalizedPath[..^2];
        if (normalizedPath == string.Empty)
            return "/";

        // Remove unnecessary path components
        normalizedPath = normalizedPath.Replace("/./", "/");

        // Remove trailing separator
        if (normalizedPath.EndsWith('/') && normalizedPath != "/")
            normalizedPath = normalizedPath[..^1];

        return normalizedPath;
    }

    public static string[] SplitArguments(string arguments)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        var argumentStarted = false;

        for (int i = 0; i < arguments.Length; i++)
        {
            var c = arguments[i];

            if ((c == ' ' || c == '\t') && !inQuotes)
            {
                if (argumentStarted)
                {
                    result.Add(current.ToString());
                    current.Clear();
                    argumentStarted = false;
                }

                continue;
            }

            argumentStarted = true;

            if (c == '\\')
            {
                var backslashCount = 0;

                while (i < arguments.Length && arguments[i] == '\\')
                {
                    backslashCount++;
                    i++;
                }

                if (i == arguments.Length || arguments[i] != '"')
                {
                    current.Append('\\', backslashCount);
                    i--;
                    continue;
                }

                current.Append('\\', backslashCount / 2);

                if (backslashCount % 2 == 0)
                    inQuotes = !inQuotes;
                else
                    current.Append('"');

                continue;
            }

            if (c == '"')
            {
                if (inQuotes && i + 1 < arguments.Length && arguments[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else                
                    inQuotes = !inQuotes;                

                continue;
            }

            current.Append(c);
        }

        if (argumentStarted)
            result.Add(current.ToString());

        return result.ToArray();
    }
}
