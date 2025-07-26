using Microsoft.Extensions.Logging;

namespace vcxproj2cmake;

record CMakeConfigDependentSetting
{
    public OrderedDictionary<Config, string> Values { get; }
    public string SettingName { get; }
    public string DefaultValue { get; }

    public CMakeConfigDependentSetting(string settingName, string defaultValue)
    {
        Values = [];
        SettingName = settingName;
        DefaultValue = defaultValue;
    }

    public CMakeConfigDependentSetting(
        MSBuildConfigDependentSetting<string> settings,
        IEnumerable<MSBuildProjectConfig> projectConfigurations,
        ILogger logger)
    {
        if (settings.Values.Count == 0)
        {
            Values = [];
            SettingName = settings.SettingName;
            DefaultValue = settings.DefaultValue;
            return;
        }

        var effectiveSettings = new Dictionary<MSBuildProjectConfig, string>(settings.Values);
        foreach (var config in projectConfigurations)
            if (!effectiveSettings.ContainsKey(config))
                effectiveSettings[config] = settings.DefaultValue;
        
        var allSettingValues = effectiveSettings.Values.Distinct().ToArray();

        var commonSettingValue = allSettingValues.FirstOrDefault(s => effectiveSettings.All(kvp => kvp.Value == s));

        string? FilterByConfig(Config config)
        {
            return allSettingValues
                .Where(s => effectiveSettings.All(kvp => config.MatchesProjectConfig(kvp.Key) == (kvp.Value == s)))
                .FirstOrDefault(s => s != commonSettingValue);
        }

        OrderedDictionary<Config, string> values = [];

        if (commonSettingValue != null)
            values[Config.CommonConfig] = commonSettingValue;

        foreach (var config in Config.Configs)
        {
            var filteredValues = FilterByConfig(config);
            if (filteredValues != null)
                values[config] = filteredValues;
        }

        Values = values;
        SettingName = settings.SettingName;
        DefaultValue = settings.DefaultValue;

        var skippedSettings = settings.Values.Values
            .Except(values.Values)
            .ToArray();
        if (skippedSettings.Length > 0)
            logger.LogWarning($"The following values for setting {settings.SettingName} were ignored because they are specific to certain build configurations: {string.Join(", ", skippedSettings)}");
    }

    public string? GetValue(MSBuildProjectConfig projectConfig)
    {
        var config = Config.Configs.SingleOrDefault(config => config.MatchesProjectConfig(projectConfig) && Values.ContainsKey(config));
        if (config != null)
            return Values[config];        
        return Values.GetValueOrDefault(Config.CommonConfig);
    }

    public bool IsEmpty => Values.Count == 0;
}

record CMakeConfigDependentMultiSetting
{
    public OrderedDictionary<Config, string[]> Values { get; }
    public string SettingName { get; }
    public string[] DefaultValue { get; }

    public CMakeConfigDependentMultiSetting(string settingName, string[] defaultValue)
    {
        Values = [];
        SettingName = settingName;
        DefaultValue = defaultValue;
    }

    public CMakeConfigDependentMultiSetting(
        MSBuildConfigDependentSetting<string[]> settings,
        IEnumerable<MSBuildProjectConfig> projectConfigurations,
        ILogger logger)
    {
        if (settings.Values.Count == 0)
        {
            Values = [];
            SettingName = settings.SettingName;
            DefaultValue = settings.DefaultValue;
            return;
        }

        var effectiveSettings = new Dictionary<MSBuildProjectConfig, string[]>(settings.Values);
        foreach (var config in projectConfigurations)
            if (!effectiveSettings.ContainsKey(config))
                effectiveSettings[config] = settings.DefaultValue;

        var allSettingValues = effectiveSettings.Values.SelectMany(s => s).Distinct().ToArray();

        var commonSettingValues = allSettingValues.Where(s => effectiveSettings.All(kvp => kvp.Value.Contains(s))).ToArray();

        string[] FilterByConfig(Config config)
        {
            return allSettingValues
                .Where(s => effectiveSettings.All(kvp => config.MatchesProjectConfig(kvp.Key) == kvp.Value.Contains(s)))
                .Except(commonSettingValues)
                .ToArray();
        }

        OrderedDictionary<Config, string[]> values = [];

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

        var skippedSettings = settings.Values.Values
            .SelectMany(s => s)
            .Except(values.Values.SelectMany(s => s))
            .ToArray();
        if (skippedSettings.Length > 0)
            logger.LogWarning($"The following values for setting {settings.SettingName} were ignored because they are specific to certain build configurations: {string.Join(", ", skippedSettings)}");
    }

    public void AppendValue(Config config, string value)
    {
        if (!Values.ContainsKey(config))
            Values[config] = [value];
        else
            Values[config] = [.. Values[config], value];
    }

    public string[] GetValue(MSBuildProjectConfig projectConfig)
    {
        return new[] { Config.CommonConfig }
            .Concat(Config.Configs)
            .Where(config => config.MatchesProjectConfig(projectConfig) && Values.ContainsKey(config))
            .SelectMany(config => Values[config])
            .ToArray();
    }

    public bool IsEmpty => Values.Count == 0;
}