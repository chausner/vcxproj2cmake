using Microsoft.Extensions.Logging;

namespace vcxproj2cmake;

record CMakeConfigDependentSetting
{
    public OrderedDictionary<Config, CMakeExpression> Values { get; }
    public string SettingName { get; }
    public CMakeExpression DefaultValue { get; }

    public CMakeConfigDependentSetting(string settingName, CMakeExpression defaultValue)
    {
        Values = [];
        SettingName = settingName;
        DefaultValue = defaultValue;
    }

    public CMakeConfigDependentSetting(
        MSBuildConfigDependentSetting<CMakeExpression> settings,
        IEnumerable<MSBuildProjectConfig> projectConfigurations,
        ILogger logger)
    {
        static bool HasContent(CMakeExpression? expression) => expression != null && expression.Value != string.Empty;

        var filteredSettingValues = settings.Values.Where(kvp => projectConfigurations.Contains(kvp.Key)).ToArray();

        if (filteredSettingValues.Length == 0)
        {
            Values = [];
            SettingName = settings.SettingName;
            DefaultValue = settings.DefaultValue;
            return;
        }

        var effectiveSettings = new Dictionary<MSBuildProjectConfig, CMakeExpression>(filteredSettingValues);
        foreach (var config in projectConfigurations)
            if (!effectiveSettings.ContainsKey(config))
                effectiveSettings[config] = settings.DefaultValue;
        
        var allSettingValues = effectiveSettings.Values.Distinct().ToArray();

        var commonSettingValue = allSettingValues.FirstOrDefault(s => effectiveSettings.All(kvp => kvp.Value == s));

        CMakeExpression? FilterByConfig(Config config)
        {
            return allSettingValues
                .Where(s => effectiveSettings.All(kvp => config.MatchesProjectConfig(kvp.Key) == (kvp.Value == s)))
                .FirstOrDefault(s => s != commonSettingValue);
        }

        OrderedDictionary<Config, CMakeExpression> values = [];

        if (HasContent(commonSettingValue))
            values[Config.CommonConfig] = commonSettingValue!;

        foreach (var config in Config.Configs)
        {
            var filteredValues = FilterByConfig(config);
            if (HasContent(filteredValues))
                values[config] = filteredValues!;
        }

        Values = values;
        SettingName = settings.SettingName;
        DefaultValue = settings.DefaultValue;

        var skippedSettings = filteredSettingValues.Select(kvp => kvp.Value)
            .Where(HasContent)
            .Except(values.Values)
            .ToArray();
        if (skippedSettings.Length > 0)
            logger.LogWarning($"The following values for setting {settings.SettingName} were ignored because they are specific to certain build configurations: {string.Join(", ", skippedSettings.Select(e => e.ToString()))}");
    }

    public CMakeConfigDependentSetting(
        MSBuildConfigDependentSetting<string> settings,
        IEnumerable<MSBuildProjectConfig> projectConfigurations,
        ILogger logger)
        : this(
            new MSBuildConfigDependentSetting<CMakeExpression>(
                settings.SettingName,
                settings.DefaultValue != null ? CMakeExpression.Literal(settings.DefaultValue) : CMakeExpression.Literal(string.Empty),
                settings.Values.ToDictionary(kvp => kvp.Key, kvp => CMakeExpression.Literal(kvp.Value))),
            projectConfigurations,
            logger)
    {
    }

    public CMakeExpression? GetValue(MSBuildProjectConfig projectConfig)
    {
        var config = Config.Configs.SingleOrDefault(config => config.MatchesProjectConfig(projectConfig) && Values.ContainsKey(config));
        if (config != null)
            return Values[config];        
        return Values.GetValueOrDefault(Config.CommonConfig);
    }

    public CMakeExpression ToCMakeExpression()
    {
        return CMakeExpression.Expression(
            string.Join(string.Empty,
            Values.Select(kvp => kvp.Key.Apply(kvp.Value).Value)));
    }

    public bool IsEmpty => Values.Count == 0;
}

record CMakeConfigDependentMultiSetting
{
    public OrderedDictionary<Config, CMakeExpression[]> Values { get; }
    public string SettingName { get; }
    public CMakeExpression[] DefaultValue { get; }

    public CMakeConfigDependentMultiSetting(string settingName, CMakeExpression[] defaultValue)
    {
        Values = [];
        SettingName = settingName;
        DefaultValue = defaultValue;
    }

    public CMakeConfigDependentMultiSetting(
        MSBuildConfigDependentSetting<CMakeExpression[]> settings,
        IEnumerable<MSBuildProjectConfig> projectConfigurations,
        ILogger logger)
    {
        var filteredSettingValues = settings.Values.Where(kvp => projectConfigurations.Contains(kvp.Key)).ToArray();

        if (filteredSettingValues.Length == 0)
        {
            Values = [];
            SettingName = settings.SettingName;
            DefaultValue = settings.DefaultValue;
            return;
        }

        var effectiveSettings = new Dictionary<MSBuildProjectConfig, CMakeExpression[]>(filteredSettingValues);
        foreach (var config in projectConfigurations)
            if (!effectiveSettings.ContainsKey(config))
                effectiveSettings[config] = settings.DefaultValue;

        var allSettingValues = effectiveSettings.Values.SelectMany(s => s).Distinct().ToArray();

        var commonSettingValues = allSettingValues.Where(s => effectiveSettings.All(kvp => kvp.Value.Contains(s))).ToArray();

        CMakeExpression[] FilterByConfig(Config config)
        {
            return allSettingValues
                .Where(s => effectiveSettings.All(kvp => config.MatchesProjectConfig(kvp.Key) == kvp.Value.Contains(s)))
                .Except(commonSettingValues)
                .ToArray();
        }

        OrderedDictionary<Config, CMakeExpression[]> values = [];

        if (commonSettingValues.Length > 0)
            values[Config.CommonConfig] = commonSettingValues;

        foreach (var config in Config.Configs)
        {
            var filteredValues = FilterByConfig(config);
            if (filteredValues.Length > 0)
                values[config] = filteredValues;
        }

        Values = values;
        SettingName = settings.SettingName;
        DefaultValue = settings.DefaultValue;

        var skippedSettings = filteredSettingValues.Select(kvp => kvp.Value)
            .SelectMany(s => s)
            .Except(values.Values.SelectMany(s => s))
            .ToArray();
        if (skippedSettings.Length > 0)
            logger.LogWarning($"The following values for setting {settings.SettingName} were ignored because they are specific to certain build configurations: {string.Join(", ", skippedSettings.Select(e => e.ToString()))}");
    }

    public CMakeConfigDependentMultiSetting(
        MSBuildConfigDependentSetting<string[]> settings,
        IEnumerable<MSBuildProjectConfig> projectConfigurations,
        ILogger logger)
        : this(
            new MSBuildConfigDependentSetting<CMakeExpression[]>(
                settings.SettingName,
                settings.DefaultValue.Select(value => CMakeExpression.Literal(value)).ToArray(),
                settings.Values.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Select(value => CMakeExpression.Literal(value)).ToArray())),
            projectConfigurations,
            logger)
    {
    }

    public void AppendValue(Config config, CMakeExpression value)
    {
        if (!Values.ContainsKey(config))
            Values[config] = [value];
        else
            Values[config] = [.. Values[config], value];
    }

    public CMakeExpression[] GetValue(MSBuildProjectConfig projectConfig)
    {
        return new[] { Config.CommonConfig }
            .Concat(Config.Configs)
            .Where(config => config.MatchesProjectConfig(projectConfig) && Values.ContainsKey(config))
            .SelectMany(config => Values[config])
            .ToArray();
    }

    public CMakeExpression ToCMakeExpression()
    {
        return CMakeExpression.Expression(
            string.Join(' ',
            Values.SelectMany(kvp =>
            kvp.Value.Select(value => kvp.Key.Apply(value).Value))));
    }

    public bool IsEmpty => Values.Count == 0;
}
