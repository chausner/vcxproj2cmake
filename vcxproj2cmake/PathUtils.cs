namespace vcxproj2cmake;

static class PathUtils
{
    public static string NormalizePathSeparators(string path)
    {
        return path.Replace('\\', System.IO.Path.DirectorySeparatorChar)
                   .Replace('/', System.IO.Path.DirectorySeparatorChar);
    }
}
