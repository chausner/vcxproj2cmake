using System.Reflection;

namespace vcxproj2cmake;

record ConanPackage(string PackageName, string CMakeConfigName, string CMakeTargetName);

class ConanPackageInfoRepository
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
                .Select(tokens =>
                {
                    var packageName = tokens[0];
                    var cmakeConfigName = !string.IsNullOrWhiteSpace(tokens[1]) ? tokens[1] : packageName;
                    var cmakeTargetName = !string.IsNullOrWhiteSpace(tokens[2])
                        ? tokens[2]
                        : $"{packageName}::{packageName}";
                    return (packageName, new ConanPackage(packageName, cmakeConfigName, cmakeTargetName));
                })
                .ToDictionary();
    }

    public ConanPackage GetConanPackageInfo(string packageName)
    {
        return conanPackageInfo.GetValueOrDefault(packageName, new ConanPackage(packageName, packageName, $"{packageName}::{packageName}"));
    }
}
