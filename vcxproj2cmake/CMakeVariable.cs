namespace vcxproj2cmake;

abstract class CMakeVariable
{
    public abstract string[] ValidValues { get; }

    public virtual CMakeExpression GetExpression(string value, CMakeExpression expr)
    {
        return CMakeExpression.Expression($"$<{GetConditionExpression(value).Value}:{expr.Value}>");
    }

    public abstract CMakeExpression GetConditionExpression(string value);

    public abstract string GetValueForProjectConfig(MSBuildProjectConfig projectConfig);

    public abstract bool IsMSBuildProjectConfigSupported(MSBuildProjectConfig projectConfig);

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

    public override string GetValueForProjectConfig(MSBuildProjectConfig projectConfig)
    {
        // TODO: use UseDebugLibraries instead of relying on the project configuration name

        if (projectConfig.Name.StartsWith("Debug|"))
            return "Debug";
        else if (projectConfig.Name.StartsWith("Release|"))
            return "Release";
        else
            throw new ArgumentException(
                $"Unsupported project configuration: '{projectConfig.Name}'. " +
                $"Expected to start with 'Debug|' or 'Release|'.");
    }

    public override bool IsMSBuildProjectConfigSupported(MSBuildProjectConfig projectConfig)
    {
        return projectConfig.Name.StartsWith("Debug|") || projectConfig.Name.StartsWith("Release|");
    }
}

class CompilerArchitectureIdCMakeVariable : CMakeVariable
{
    internal const string CompilerArchitectureIdVariablePlaceholder = "__VCXPROJ2CMAKE_COMPILER_ARCHITECTURE_ID_VARIABLE__";

    public override string[] ValidValues => ["X86", "x64", "ARMV7", "ARM64"];

    public override CMakeExpression GetConditionExpression(string value)
    {
        if (!ValidValues.Contains(value))
            throw new ArgumentException($"Invalid value for variable: '{value}'. Expected one of: {string.Join(", ", ValidValues)}");

        return CMakeExpression.Expression($"$<STREQUAL:${{{CompilerArchitectureIdVariablePlaceholder}}},{value}>");
    }

    public override string GetValueForProjectConfig(MSBuildProjectConfig projectConfig)
    {
        // TODO: use Platform instead of relying on the project configuration name

        if (projectConfig.Name.EndsWith("|Win32") || projectConfig.Name.EndsWith("|x86"))
            return "X86";
        else if (projectConfig.Name.EndsWith("|x64"))
            return "x64";
        else if (projectConfig.Name.EndsWith("|ARM32"))
            return "ARMV7";
        else if (projectConfig.Name.EndsWith("|ARM64"))
            return "ARM64";
        else
            throw new ArgumentException(
                $"Unsupported project configuration: '{projectConfig.Name}'. " +
                $"Expected to end with '|Win32', '|x86', '|x64', '|ARM32', or '|ARM64'.");
    }

    public override bool IsMSBuildProjectConfigSupported(MSBuildProjectConfig projectConfig)
    {
        return 
            projectConfig.Name.EndsWith("|Win32") ||
            projectConfig.Name.EndsWith("|x86") ||
            projectConfig.Name.EndsWith("|x64") ||
            projectConfig.Name.EndsWith("|ARM32") ||
            projectConfig.Name.EndsWith("|ARM64");
    }
}
