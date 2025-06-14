using System.Text.RegularExpressions;

namespace vcxproj2cmake;

record Config(Regex MSBuildProjectConfigPattern, string CMakeExpression)
{
    public static readonly Config CommonConfig = new Config(new(@".*"), "{0}");

    public static readonly Config[] Configs =
    [
        new Config(new(@"^Debug\|"), "$<$<CONFIG:Debug>:{0}>"),
        new Config(new(@"^Release\|"), "$<$<CONFIG:Release>:{0}>"),
        new Config(new(@"\|(Win32|x86)$"), "$<$<STREQUAL:$<CMAKE_CXX_COMPILER_ARCHITECTURE_ID>,X86>:{0}>"),
        new Config(new(@"\|x64$"), "$<$<STREQUAL:$<CMAKE_CXX_COMPILER_ARCHITECTURE_ID>,x64>:{0}>"),
        new Config(new(@"\|ARM32$"), "$<$<STREQUAL:$<CMAKE_CXX_COMPILER_ARCHITECTURE_ID>,ARM>:{0}>"),
        new Config(new(@"\|ARM64$"), "$<$<STREQUAL:$<CMAKE_CXX_COMPILER_ARCHITECTURE_ID>,ARM64>:{0}>")
    ];

    public bool MatchesProjectConfigName(string projectConfig)
    {
        return MSBuildProjectConfigPattern.IsMatch(projectConfig);
    }

    public static bool IsMSBuildProjectConfigNameSupported(string projectConfig)
    {
        return Regex.IsMatch(projectConfig, @"^(Debug|Release)\|(Win32|x86|x64|ARM32|ARM64)$");
    }
}
