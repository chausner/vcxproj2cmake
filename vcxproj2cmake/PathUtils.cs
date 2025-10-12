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
}
