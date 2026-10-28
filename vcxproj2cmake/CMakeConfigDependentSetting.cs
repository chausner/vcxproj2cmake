using Microsoft.Extensions.Logging;

namespace vcxproj2cmake;

record CMakeConfigDependentSetting
{
    public Dictionary<MSBuildProjectConfig, CMakeExpression> Values { get; }
    public string SettingName { get; }
    public CMakeExpression DefaultValue { get; }
    public MSBuildProject MSBuildProject { get; }

    readonly ILogger logger;

    public CMakeConfigDependentSetting(string settingName, CMakeExpression defaultValue, MSBuildProject msbuildProject, ILogger logger)
    {
        Values = [];
        SettingName = settingName;
        DefaultValue = defaultValue;
        MSBuildProject = msbuildProject;
        this.logger = logger;
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

        foreach (var config in projectConfigurations)
            if (!effectiveSettings.ContainsKey(config))
                effectiveSettings[config] = settings.DefaultValue;

        Values = effectiveSettings;
        SettingName = settings.SettingName;
        DefaultValue = settings.DefaultValue;
        MSBuildProject = msbuildProject;
        this.logger = logger;
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
        if (IsEmpty)
            return [];

        var firstValue = Values.Values.First();
        if (Values.Values.All(value => value == firstValue))
            return [firstValue];

        CMakeVariable.ValidateMapping(SettingName, Values, MSBuildProject, (left, right) => left == right);

        var variableGroups = CMakeVariable.AllVariables
            .Select(variable => (
                Variable: variable,
                Groups: Values.GroupBy(kvp => variable.GetValueForProjectConfig(kvp.Key, MSBuildProject)).ToArray()))
            .Where(mapping => mapping.Groups.Length > 1)
            .ToArray();

        var singleConditionVariables = variableGroups
            .Where(mapping => mapping.Groups.All(group => group.All(kvp => kvp.Value == group.First().Value)))
            .ToArray();

        if (singleConditionVariables.Length >= 2)
            logger.LogWarning($"Multiple CMake variables can be used to distinguish between values for setting {SettingName}. One of the variables will be used, but this may not be the intended behavior.");

        if (singleConditionVariables.Length >= 1)
        {
            var (variable, groups) = singleConditionVariables[0];
            return groups
                .Where(group => group.First().Value.Value != string.Empty)
                .Select(group => variable.GetExpression(group.Key, group.First().Value))
                .ToArray();
        }

        return Values
            .Where(kvp => kvp.Value.Value != string.Empty)
            .Select(kvp =>
            {
                var conditions = variableGroups.Select(mapping => mapping.Variable.GetConditionExpression(
                    mapping.Variable.GetValueForProjectConfig(kvp.Key, MSBuildProject)).Value);
                return CMakeExpression.Expression($"$<$<AND:{string.Join(',', conditions)}>:{kvp.Value.Value}>");
            })
            .Distinct()
            .ToArray();
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

    readonly ILogger logger;

    public CMakeConfigDependentMultiSetting(string settingName, CMakeExpression[] defaultValue, MSBuildProject msbuildProject, ILogger logger)
    {
        Values = [];
        SettingName = settingName;
        DefaultValue = defaultValue;
        MSBuildProject = msbuildProject;
        this.logger = logger;
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

        foreach (var config in projectConfigurations)
            if (!effectiveSettings.ContainsKey(config))
                effectiveSettings[config] = settings.DefaultValue;

        Values = effectiveSettings;
        SettingName = settings.SettingName;
        DefaultValue = settings.DefaultValue;
        MSBuildProject = msbuildProject;
        this.logger = logger;
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
            Values[config] = [value];
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
            Values[config] = [value];
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
        if (IsEmpty)
            return [];

        var firstValue = Values.Values.First();
        if (Values.Values.All(value => value.SequenceEqual(firstValue)))
            return firstValue;

        // Validate complete lists before splitting them into individual entries so
        // differences in ordering or multiplicity cannot hide an ambiguous mapping.
        CMakeVariable.ValidateMapping(SettingName, Values, MSBuildProject, (left, right) => left.SequenceEqual(right));

        var commonExpressions = firstValue
            .Where(value => Values.Values.All(values => values.Contains(value)))
            .Distinct()
            .ToArray();
        List<CMakeExpression> exprs = [.. commonExpressions];
        var uniqueValues = Values.Values.SelectMany(values => values).Except(commonExpressions);
        var empty = CMakeExpression.Expression(string.Empty);

        foreach (var value in uniqueValues)
        {
            var setting = new CMakeConfigDependentSetting(SettingName, empty, MSBuildProject, logger);
            foreach (var (projectConfig, values) in Values)
                setting.Values.Add(projectConfig, values.Contains(value) ? value : empty);
            exprs.AddRange(setting.ToCMakeExpressions());
        }

        return exprs.ToArray();
    }

    public CMakeExpression[] CMakeExpressions => ToCMakeExpressions();

    public bool IsEmpty => Values.Values.All(exprs => exprs.Length == 0);
}
