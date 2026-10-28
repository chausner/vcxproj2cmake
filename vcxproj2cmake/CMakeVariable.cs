namespace vcxproj2cmake;

abstract class CMakeVariable
{
    public abstract string[] ValidValues { get; }

    public virtual CMakeExpression GetExpression(string value, CMakeExpression expr)
    {
        return CMakeExpression.Expression($"$<{GetConditionExpression(value).Value}:{expr.Value}>");
    }

    public abstract CMakeExpression GetConditionExpression(string value);

    public abstract string GetValueForProjectConfig(MSBuildProjectConfig projectConfig, MSBuildProject project);

    public static void ValidateMapping<TValue>(
        string settingName,
        IReadOnlyDictionary<MSBuildProjectConfig, TValue> values,
        MSBuildProject project,
        Func<TValue, TValue, bool> valuesEqual)
    {
        var entries = values.ToArray();

        for (var i = 0; i < entries.Length; i++)        
            for (var j = i + 1; j < entries.Length; j++)
            {
                var (config1, value1) = entries[i];
                var (config2, value2) = entries[j];
                
                if (!valuesEqual(value1, value2) && AllVariables.All(variable =>
                    variable.GetValueForProjectConfig(config1, project) ==
                    variable.GetValueForProjectConfig(config2, project)))
                {
                    throw new CatastrophicFailureException($"Cannot convert setting {settingName} to a CMake expression because it has multiple values and no CMake variable can be used to distinguish between them.");
                }
            }        
    }

    public static readonly CMakeVariable[] AllVariables =
    [
        new BuildTypeCMakeVariable(),
        new CompilerArchitectureIdCMakeVariable()
    ];
}

class BuildTypeCMakeVariable : CMakeVariable
{
    public override string[] ValidValues => ["Debug", "Release"];

    public override CMakeExpression GetConditionExpression(string value)
    {
        if (!ValidValues.Contains(value))
            throw new ArgumentException($"Invalid value for variable: '{value}'. Expected one of: {string.Join(", ", ValidValues)}");

        return CMakeExpression.Expression($"$<CONFIG:{value}>");
    }

    public override string GetValueForProjectConfig(MSBuildProjectConfig projectConfig, MSBuildProject project)
    {
        var useDebugLibraries = project.UseDebugLibraries.GetEffectiveValue(projectConfig);

        return useDebugLibraries.ToLowerInvariant() switch
        {
            "true" => "Debug",
            "false" => "Release",
            _ => throw new CatastrophicFailureException($"Unsupported value for UseDebugLibraries: '{useDebugLibraries}'. Expected 'true' or 'false'.")
        };
    }
}

class CompilerArchitectureIdCMakeVariable : CMakeVariable
{
    internal const string VariablePlaceholder = "__VCXPROJ2CMAKE_COMPILER_ARCHITECTURE_ID_VARIABLE__";

    public override string[] ValidValues => ["X86", "x64", "ARMV7", "ARM64"];

    public override CMakeExpression GetConditionExpression(string value)
    {
        if (!ValidValues.Contains(value))
            throw new ArgumentException($"Invalid value for variable: '{value}'. Expected one of: {string.Join(", ", ValidValues)}");

        return CMakeExpression.Expression($"$<STREQUAL:${{{VariablePlaceholder}}},{value}>");
    }

    public override string GetValueForProjectConfig(MSBuildProjectConfig projectConfig, MSBuildProject project)
    {
        return projectConfig.Platform switch
        {
            "Win32" => "X86",
            "x86" => "X86",
            "x64" => "x64",
            "ARM32" => "ARMV7",
            "ARM64" => "ARM64",
            _ => throw new CatastrophicFailureException($"Unsupported project configuration platform: '{projectConfig.Platform}'. Expected one of: 'Win32', 'x86', 'x64', 'ARM32', or 'ARM64'.")
        };
    }
}
