using System.Text.RegularExpressions;

namespace vcxproj2cmake;

record Config(Regex MSBuildProjectConfigPattern, string Prefix, string Suffix)
{
    public static readonly Config CommonConfig = new Config(new(@".*"), string.Empty, string.Empty);

    public static readonly Config[] Configs =
    [
        new Config(new(@"^Debug\|"), "$<$<CONFIG:Debug>:", ">"),
        new Config(new(@"^Release\|"), "$<$<CONFIG:Release>:", ">"),
        new Config(new(@"\|(Win32|x86)$"), "$<$<STREQUAL:${CMAKE_CXX_COMPILER_ARCHITECTURE_ID},X86>:", ">"),
        new Config(new(@"\|x64$"), "$<$<STREQUAL:${CMAKE_CXX_COMPILER_ARCHITECTURE_ID},x64>:", ">"),
        new Config(new(@"\|ARM32$"), "$<$<STREQUAL:${CMAKE_CXX_COMPILER_ARCHITECTURE_ID},ARMV7>:", ">"),
        new Config(new(@"\|ARM64$"), "$<$<STREQUAL:${CMAKE_CXX_COMPILER_ARCHITECTURE_ID},ARM64>:", ">")
    ];

    public bool MatchesProjectConfig(MSBuildProjectConfig projectConfig)
    {
        return MSBuildProjectConfigPattern.IsMatch(projectConfig.Name);
    }

    public CMakeExpression Apply(CMakeExpression value)
    {
        return CMakeExpression.Expression(Prefix + value.Value + Suffix);
    }

    public static bool IsMSBuildProjectConfigSupported(MSBuildProjectConfig projectConfig)
    {
        return Regex.IsMatch(projectConfig.Name, @"^(Debug|Release)\|(Win32|x86|x64|ARM32|ARM64)$");
    }
}
