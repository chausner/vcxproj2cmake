using System.Text.RegularExpressions;

namespace vcxproj2cmake;

class Config(Regex msBuildProjectConfigPattern, Func<CMakeExpression, CMakeExpression> applyFunc)
{
    public static readonly Config CommonConfig = new Config(new(@".*"), expr => expr);

    public static readonly Config[] Configs =
    [
        new Config(new(@"^Debug\|"), expr => CMakeExpression.Expression($"$<$<CONFIG:Debug>:{expr.Value}>")),
        new Config(new(@"^Release\|"), expr => CMakeExpression.Expression($"$<$<CONFIG:Release>:{expr.Value}>")),
        new Config(new(@"\|(Win32|x86)$"), expr => CMakeExpression.Expression($"$<$<STREQUAL:${{CMAKE_CXX_COMPILER_ARCHITECTURE_ID}},X86>:{expr.Value}>")),
        new Config(new(@"\|x64$"), expr => CMakeExpression.Expression($"$<$<STREQUAL:${{CMAKE_CXX_COMPILER_ARCHITECTURE_ID}},x64>:{expr.Value}>")),
        new Config(new(@"\|ARM32$"), expr => CMakeExpression.Expression($"$<$<STREQUAL:${{CMAKE_CXX_COMPILER_ARCHITECTURE_ID}},ARMV7>:{expr.Value}>")),
        new Config(new(@"\|ARM64$"), expr => CMakeExpression.Expression($"$<$<STREQUAL:${{CMAKE_CXX_COMPILER_ARCHITECTURE_ID}},ARM64>:{expr.Value}>"))
    ];

    public bool MatchesProjectConfig(MSBuildProjectConfig projectConfig)
    {
        return msBuildProjectConfigPattern.IsMatch(projectConfig.Name);
    }

    public CMakeExpression Apply(CMakeExpression value)
    {
        return applyFunc(value);
    }

    public static bool IsMSBuildProjectConfigSupported(MSBuildProjectConfig projectConfig)
    {
        return Regex.IsMatch(projectConfig.Name, @"^(Debug|Release)\|(Win32|x86|x64|ARM32|ARM64)$");
    }
}
