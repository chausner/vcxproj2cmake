namespace vcxproj2cmake;

static class PathUtils
{
    public static string NormalizePathSeparators(string path)
    {
        return path.Replace('\\', Path.DirectorySeparatorChar)
                   .Replace('/', Path.DirectorySeparatorChar);
    }
}
