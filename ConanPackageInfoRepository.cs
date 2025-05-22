using System.Collections.ObjectModel;
using System.Reflection;

record ConanPackage(string PackageName, string CMakeConfigName, string CMakeTargetName);

internal class ConanPackageInfoRepository
{
    static readonly Dictionary<string, ConanPackage> conanPackageInfo = LoadConanPackageInfo();

    static Dictionary<string, ConanPackage> LoadConanPackageInfo()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("vcxproj2cmake.Resources.conan-packages.csv")!;
        using var streamReader = new StreamReader(stream);

        return
            streamReader.ReadToEnd()
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split(','))
            .Select(tokens => (tokens[0], new ConanPackage(tokens[0], tokens[1], tokens[2])))
            .ToDictionary();
    }

    public ConanPackage GetConanPackageInfo(string packageName)
    {
        return conanPackageInfo.GetValueOrDefault(packageName, new ConanPackage(packageName, packageName, $"{packageName}::{packageName}"));
    }
}
