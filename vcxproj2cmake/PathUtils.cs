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

    public static bool IsCMakePathAbsolute(string path)
    {
        // if a path starts with a CMake variable or generator expression, we just assume that it resolves to an absolute path
        return path.StartsWith("${") || path.StartsWith("$<") || Path.IsPathFullyQualified(path);
    }

    public static string? CanonicalizeCMakePath(string path, string absoluteProjectPath)
    {
        const string currentSourceDir = "${CMAKE_CURRENT_SOURCE_DIR}";

        string currentSourceDirPath = Path.GetDirectoryName(absoluteProjectPath)!;

        if (path == currentSourceDir)
            return currentSourceDirPath;
        else if (path.StartsWith(currentSourceDir + "/", StringComparison.Ordinal))
            return Path.GetFullPath(Path.Combine(currentSourceDirPath, path[(currentSourceDir.Length + 1)..]));
        else if (path.StartsWith("${", StringComparison.Ordinal))
            return null;
        else if (Path.IsPathFullyQualified(path))
            return Path.GetFullPath(path);
        else
            return Path.GetFullPath(Path.Combine(currentSourceDirPath, path));
    }
}
