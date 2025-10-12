using System.Text.RegularExpressions;

namespace vcxproj2cmake;

record Config(Regex MSBuildProjectConfigPattern, string CMakeExpression)
{
    public static readonly Config CommonConfig = new Config(new(@".*"), "{0}");

    public static readonly Config[] Configs =
    [
        new Config(new(@"^Debug\|"), "$<$<CONFIG:Debug>:{0}>"),
        new Config(new(@"^Release\|"), "$<$<CONFIG:Release>:{0}>"),
        new Config(new(@"\|(Win32|x86)$"), "$<$<STREQUAL:${CMAKE_CXX_COMPILER_ARCHITECTURE_ID},X86>:{0}>"),
        new Config(new(@"\|x64$"), "$<$<STREQUAL:${CMAKE_CXX_COMPILER_ARCHITECTURE_ID},x64>:{0}>"),
        new Config(new(@"\|ARM32$"), "$<$<STREQUAL:${CMAKE_CXX_COMPILER_ARCHITECTURE_ID},ARMV7>:{0}>"),
        new Config(new(@"\|ARM64$"), "$<$<STREQUAL:${CMAKE_CXX_COMPILER_ARCHITECTURE_ID},ARM64>:{0}>")
    ];

    public bool MatchesProjectConfig(MSBuildProjectConfig projectConfig)
    {
        return MSBuildProjectConfigPattern.IsMatch(projectConfig.Name);
    }

    public static bool IsMSBuildProjectConfigSupported(MSBuildProjectConfig projectConfig)
    {
        return Regex.IsMatch(projectConfig.Name, @"^(Debug|Release)\|(Win32|x86|x64|ARM32|ARM64)$");
    }
}
