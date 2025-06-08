using Microsoft.Extensions.Logging;
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

record ConfigDependentSetting
{
    public required OrderedDictionary<Config, string> Values { get; init; }
    public required string SettingName { get; init; }
    public required string DefaultValue { get; init; }

    public static ConfigDependentSetting Parse(
        Dictionary<string, string>? settings,
        string settingName,
        string defaultValue,
        IEnumerable<string> projectConfigurations,
        ILogger logger)
    {
        if (settings == null || settings.Count == 0)
            return new ConfigDependentSetting
            {
                Values = [],
                SettingName = settingName,
                DefaultValue = defaultValue
            };

        var effectiveSettings = new Dictionary<string, string>(settings);
        foreach (var config in projectConfigurations)
            if (!effectiveSettings.ContainsKey(config))
                effectiveSettings[config] = defaultValue;
        
        var allSettingValues = effectiveSettings.Values.Distinct().ToArray();

        var commonSettingValue = allSettingValues.FirstOrDefault(s => effectiveSettings.All(kvp => kvp.Value == s));

        string? FilterByConfig(Config config)
        {
            return allSettingValues
                .Where(s => effectiveSettings.All(kvp => config.MatchesProjectConfigName(kvp.Key) == (kvp.Value == s)))
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

        var result = new ConfigDependentSetting { Values = values, SettingName = settingName, DefaultValue = defaultValue };

        var skippedSettings = settings.Values
            .Except(result.Values.Values)
            .ToArray();
        if (skippedSettings.Length > 0)
            logger.LogWarning($"The following values for setting {settingName} were ignored because they are specific to certain build configurations: {string.Join(", ", skippedSettings)}");

        return result;
    }

    public string? GetValue(string projectConfig)
    {
        var config = Config.Configs.SingleOrDefault(config => config.MatchesProjectConfigName(projectConfig) && Values.ContainsKey(config));
        if (config != null)
            return Values[config];        
        return Values.GetValueOrDefault(Config.CommonConfig);
    }

    public bool IsEmpty => Values.Count == 0;
}

record ConfigDependentMultiSetting
{
    public required OrderedDictionary<Config, string[]> Values { get; init; }
    public required string SettingName { get; init; }
    public required string[] DefaultValue { get; init; }

    public static ConfigDependentMultiSetting Parse(
        Dictionary<string, string[]>? settings,
        string settingName,
        string[] defaultValue,
        IEnumerable<string> projectConfigurations,
        ILogger logger)
    {
        if (settings == null || settings.Count == 0)
            return new ConfigDependentMultiSetting
            {
                Values = [],
                SettingName = settingName,
                DefaultValue = defaultValue
            };

        var effectiveSettings = new Dictionary<string, string[]>(settings);
        foreach (var config in projectConfigurations)
            if (!effectiveSettings.ContainsKey(config))
                effectiveSettings[config] = defaultValue;

        var allSettingValues = effectiveSettings.Values.SelectMany(s => s).Distinct().ToArray();

        var commonSettingValues = allSettingValues.Where(s => effectiveSettings.All(kvp => kvp.Value.Contains(s))).ToArray();

        string[] FilterByConfig(Config config)
        {
            return allSettingValues
                .Where(s => effectiveSettings.All(kvp => config.MatchesProjectConfigName(kvp.Key) == kvp.Value.Contains(s)))
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

        var result = new ConfigDependentMultiSetting { Values = values, SettingName = settingName, DefaultValue = defaultValue };

        var skippedSettings = settings.Values
            .SelectMany(s => s)
            .Except(result.Values.Values.SelectMany(s => s))
            .ToArray();
        if (skippedSettings.Length > 0)
            logger.LogWarning($"The following values for setting {settingName} were ignored because they are specific to certain build configurations: {string.Join(", ", skippedSettings)}");

        return result;
    }

    public string[] GetValue(string projectConfig)
    {
        return new[] { Config.CommonConfig }
            .Concat(Config.Configs)
            .Where(config => config.MatchesProjectConfigName(projectConfig) && Values.ContainsKey(config))
            .SelectMany(config => Values[config])
            .ToArray();
    }

    public bool IsEmpty => Values.Count == 0;
}
