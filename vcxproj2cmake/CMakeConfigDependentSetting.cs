using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace vcxproj2cmake;

record CMakeConfigDependentSetting
{
    public Dictionary<MSBuildProjectConfig, CMakeExpression> Values { get; }
    public string SettingName { get; }
    public CMakeExpression DefaultValue { get; }
    public MSBuildProject MSBuildProject { get; }

    public CMakeConfigDependentSetting(string settingName, CMakeExpression defaultValue, MSBuildProject msbuildProject)
    {
        Values = [];
        SettingName = settingName;
        DefaultValue = defaultValue;
        MSBuildProject = msbuildProject;
    }

    public CMakeConfigDependentSetting(
        MSBuildConfigDependentSetting<CMakeExpression> settings,
        IEnumerable<MSBuildProjectConfig> projectConfigurations,
        MSBuildProject msbuildProject,
        ILogger logger)
    {
        var effectiveSettings = settings.Values
            .Where(kvp => projectConfigurations.Contains(kvp.Key))
            .ToDictionary();
        // TODO: is this needed?
        foreach (var config in projectConfigurations)
            if (!effectiveSettings.ContainsKey(config))
                effectiveSettings[config] = settings.DefaultValue;

        Values = effectiveSettings;
        SettingName = settings.SettingName;
        DefaultValue = settings.DefaultValue;
        MSBuildProject = msbuildProject;
    }

    public CMakeConfigDependentSetting(
        MSBuildConfigDependentSetting<string> settings,
        IEnumerable<MSBuildProjectConfig> projectConfigurations,
        MSBuildProject msbuildProject,
        ILogger logger)
        : this(
            new MSBuildConfigDependentSetting<CMakeExpression>(
                settings.SettingName,
                CMakeExpression.Literal(settings.DefaultValue ?? string.Empty),
                settings.Values.ToDictionary(kvp => kvp.Key, kvp => CMakeExpression.Literal(kvp.Value))),
            projectConfigurations,
            msbuildProject,
            logger)
    {
    }

    public CMakeExpression? GetValue(MSBuildProjectConfig projectConfig)
    {
        return Values.GetValueOrDefault(projectConfig, DefaultValue);
    }

    public CMakeExpression[] ToCMakeExpressions()
    {
        var projectConfigs = Values.Keys;

        if (Values.Values.Distinct().Count() == 1)
            if (Values.Values.First().Value != string.Empty)
                return [Values.Values.First()];
            else
                return [];

        List<CMakeVariable> variablesToConsider = [];

        foreach (var variable in CMakeVariable.AllVariables)
        {
            int numDistinctValues = projectConfigs.Select(config => variable.GetValueForProjectConfig(config, MSBuildProject)).Distinct().Count();
            if (numDistinctValues > 1)
                variablesToConsider.Add(variable);
        }

        if (variablesToConsider.Count == 0)
        {
            if (Values.Values.Distinct().Count() != 1)
                throw new CatastrophicFailureException($"Cannot convert setting {SettingName} to a CMake expression because it has multiple values and no CMake variable can be used to distinguish between them.");

            if (Values.Values.First().Value != string.Empty)
                return [Values.Values.First()];
            else
                return [];
        }

        CMakeVariable[] singleConditionVariables =
            variablesToConsider
            .Where(variable => projectConfigs.GroupBy(config => variable.GetValueForProjectConfig(config, MSBuildProject))
            .All(grouping => grouping.Select(config => Values[config]).Distinct().Count() == 1))
            .ToArray();

        if (singleConditionVariables.Length >= 2)
        {
            // TODO: log warning that multiple variables can be used to distinguish between values, but only one will be used
        }

        if (singleConditionVariables.Length >= 1)
        {
            var variable = singleConditionVariables.First();

            List<CMakeExpression> exprs = [];
            foreach (var grouping in projectConfigs.GroupBy(config => variable.GetValueForProjectConfig(config, MSBuildProject)))
            {
                var value = Values[grouping.First()];
                if (value.Value != string.Empty)
                    exprs.Add(variable.GetExpression(grouping.Key, value));
            }
            return exprs.ToArray();
        }

        {
            List<CMakeExpression> exprs = [];
            foreach (var (projectConfig, value) in Values)
            {
                if (value.Value == string.Empty)
                    continue;
                var variableValues = variablesToConsider.Select(variable => variable.GetValueForProjectConfig(projectConfig, MSBuildProject)).ToArray();
                var expr = CMakeExpression.Expression("$<$<AND:");
                foreach (var (i, (variable, variableValue)) in variablesToConsider.Zip(variableValues).Index())
                {
                    expr += variable.GetConditionExpression(variableValue);
                    if (i < variablesToConsider.Count - 1)
                        expr += CMakeExpression.Expression(",");
                }
                expr += CMakeExpression.Expression(">:" + value.Value + ">");
                exprs.Add(expr);
            }
            return exprs.ToArray();
        }
    }

    public CMakeExpression ToCMakeExpression()
    {
        return CMakeExpression.Expression(string.Join(string.Empty, ToCMakeExpressions().Select(expr => expr.Value)));
    }

    public CMakeExpression[] CMakeExpressions => ToCMakeExpressions();

    public bool IsEmpty => Values.Values.All(exprs => exprs.Value == string.Empty);
}

record CMakeConfigDependentMultiSetting
{
    public Dictionary<MSBuildProjectConfig, CMakeExpression[]> Values { get; }
    public string SettingName { get; }
    public CMakeExpression[] DefaultValue { get; }
    public MSBuildProject MSBuildProject { get; }

    public CMakeConfigDependentMultiSetting(string settingName, CMakeExpression[] defaultValue, MSBuildProject msbuildProject)
    {
        Values = [];
        SettingName = settingName;
        DefaultValue = defaultValue;
        MSBuildProject = msbuildProject;
    }

    public CMakeConfigDependentMultiSetting(
        MSBuildConfigDependentSetting<CMakeExpression[]> settings,
        IEnumerable<MSBuildProjectConfig> projectConfigurations,
        MSBuildProject msbuildProject,
        ILogger logger)
    {
        var effectiveSettings = settings.Values
            .Where(kvp => projectConfigurations.Contains(kvp.Key))
            .ToDictionary();
        // TODO: is this needed?
        foreach (var config in projectConfigurations)
            if (!effectiveSettings.ContainsKey(config))
                effectiveSettings[config] = settings.DefaultValue;       

        Values = effectiveSettings;
        SettingName = settings.SettingName;
        DefaultValue = settings.DefaultValue;     
        MSBuildProject = msbuildProject;
    }

    public CMakeConfigDependentMultiSetting(
        MSBuildConfigDependentSetting<string[]> settings,
        IEnumerable<MSBuildProjectConfig> projectConfigurations,
        MSBuildProject msbuildProject,
        ILogger logger)
        : this(
            new MSBuildConfigDependentSetting<CMakeExpression[]>(
                settings.SettingName,
                settings.DefaultValue.Select(value => CMakeExpression.Literal(value)).ToArray(),
                settings.Values.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Select(value => CMakeExpression.Literal(value)).ToArray())),
            projectConfigurations,
            msbuildProject,
            logger)
    {
    }

    public void AppendValue(MSBuildProjectConfig config, CMakeExpression value)
    { 
        if (!Values.ContainsKey(config))
            Values[config] = [value]; // TODO: is this needed?
        else
            Values[config] = [.. Values[config], value];
    }

    public void AppendValue(IEnumerable<MSBuildProjectConfig> projectConfigs, CMakeExpression value)
    {
        foreach (var projectConfig in projectConfigs)
            AppendValue(projectConfig, value);
    }

    public void AppendValueIfNotPresent(MSBuildProjectConfig config, CMakeExpression value)
    {
        if (!Values.ContainsKey(config))
            Values[config] = [value]; // TODO: is this needed?
        else
            if (!Values[config].Contains(value))
                Values[config] = [.. Values[config], value];
    }

    public void AppendValueIfNotPresent(IEnumerable<MSBuildProjectConfig> projectConfigs, CMakeExpression value)
    {
        foreach (var projectConfig in projectConfigs)
            AppendValueIfNotPresent(projectConfig, value);
    }

    public CMakeExpression[] GetValue(MSBuildProjectConfig projectConfig)
    {
        return Values.GetValueOrDefault(projectConfig, DefaultValue);
    }

    public CMakeExpression[] ToCMakeExpressions()
    {
        var projectConfigs = Values.Keys;

        if (Values.Values.Distinct(CMakeExpressionArrayEqualityComparer.Instance).Count() == 1)
            return Values.Values.First();

        Dictionary<MSBuildProjectConfig, List<CMakeExpression>> values = new(Values.Select(kvp => new KeyValuePair<MSBuildProjectConfig, List<CMakeExpression>>(kvp.Key, kvp.Value.ToList())));

        List<CMakeExpression> exprs = [];

        CMakeExpression[] commonExpressions = 
            Values.Values
            .Aggregate((acc, exprs) => acc.Intersect(exprs).ToArray())
            .ToArray();

        exprs.AddRange(commonExpressions);

        foreach (var value in values.Values)        
            foreach (var expr in commonExpressions)
                value.Remove(expr);

        var uniqueValues = values.Values.SelectMany(exprs => exprs).Distinct().ToArray();

        foreach (var value in uniqueValues)
        {
            var setting = new CMakeConfigDependentSetting("foo", CMakeExpression.Expression(string.Empty), MSBuildProject);
            foreach (var projectConfig in projectConfigs)
                setting.Values.Add(projectConfig, values[projectConfig].Contains(value) ? value : CMakeExpression.Expression(string.Empty));
            exprs.AddRange(setting.ToCMakeExpressions());
        }

        return exprs.ToArray();
    }

    public CMakeExpression[] CMakeExpressions => ToCMakeExpressions();

    public bool IsEmpty => Values.Values.All(exprs => exprs.Length == 0);
}

class CMakeExpressionArrayEqualityComparer : IEqualityComparer<CMakeExpression[]>
{
    public static readonly CMakeExpressionArrayEqualityComparer Instance = new();

    public bool Equals(CMakeExpression[]? x, CMakeExpression[]? y)
    {
        if (x == null && y == null)
            return true;
        else if (x == null || y == null)
            return false;
        else
            return x.SequenceEqual(y);
    }

    public int GetHashCode([DisallowNull] CMakeExpression[] obj)
    {
        return obj.Aggregate(0, (acc, expr) => acc ^ expr.GetHashCode());
    }
}