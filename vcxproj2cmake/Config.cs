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
        new Config(new(@"\|(Win32|x86)$"), "$<$<STREQUAL:$<TARGET_PROPERTY:ARCHITECTURE_ID>,x86>:{0}>"),
        new Config(new(@"\|x64$"), "$<$<STREQUAL:$<TARGET_PROPERTY:ARCHITECTURE_ID>,x64>:{0}>"),
        new Config(new(@"\|ARM32$"), "$<$<STREQUAL:$<TARGET_PROPERTY:ARCHITECTURE_ID>,ARM32>:{0}>"),
        new Config(new(@"\|ARM64$"), "$<$<STREQUAL:$<TARGET_PROPERTY:ARCHITECTURE_ID>,ARM64>:{0}>")
    ];

    public static bool IsMSBuildProjectConfigNameSupported(string projectConfig)
    {
        return Regex.IsMatch(projectConfig, @"^(Debug|Release)\|(Win32|x86|x64|ARM32|ARM64)$");
    }
}

record ConfigDependentSetting
{
    public required OrderedDictionary<Config, string> Values { get; init; }

    public static readonly ConfigDependentSetting Empty = new()
    {
        Values = []
    };

    public static ConfigDependentSetting Parse(Dictionary<string, string>? settings, string settingName, ILogger logger)
    {
        if (settings == null || settings.Count == 0)
            return Empty;

        var allSettingValues = settings.Values.Distinct().ToArray();

        var commonSettingValue = allSettingValues.FirstOrDefault(s => settings.All(kvp => kvp.Value == s));

        string? FilterByConfig(Config config)
        {
            return allSettingValues
                .Where(s => settings.All(kvp => config.MSBuildProjectConfigPattern.IsMatch(kvp.Key) == (kvp.Value == s)))
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

        var result = new ConfigDependentSetting { Values = values };

        var skippedSettings = settings.Values
            .Except(result.Values.Values)
            .ToArray();
        if (skippedSettings.Length > 0)
            logger.LogWarning($"The following values for setting {settingName} were ignored because they are specific to certain build configurations: {string.Join(", ", skippedSettings)}");

        return result;
    }

    public bool IsEmpty => Values.Count == 0;
}

record ConfigDependentMultiSetting
{
    public required OrderedDictionary<Config, string[]> Values { get; init; }

    public static readonly ConfigDependentMultiSetting Empty = new()
    {
        Values = []
    };

    public static ConfigDependentMultiSetting Parse(Dictionary<string, string>? settings, string settingName, Func<string, string[]> parser, ILogger logger)
    {
        if (settings == null || settings.Count == 0)
            return Empty;

        var parsedSettings = settings.ToDictionary(kvp => kvp.Key, kvp => parser(kvp.Value));

        var allSettingValues = parsedSettings.Values.SelectMany(s => s).Distinct().ToArray();

        var commonSettingValues = allSettingValues.Where(s => parsedSettings.All(kvp => kvp.Value.Contains(s))).ToArray();

        string[] FilterByConfig(Config config)
        {
            return allSettingValues
                .Where(s => parsedSettings.All(kvp => config.MSBuildProjectConfigPattern.IsMatch(kvp.Key) == kvp.Value.Contains(s)))
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

        var result = new ConfigDependentMultiSetting { Values = values };

        var skippedSettings = parsedSettings.Values
            .SelectMany(s => s)
            .Except(result.Values.Values.SelectMany(s => s))
            .ToArray();
        if (skippedSettings.Length > 0)
            logger.LogWarning($"The following values for setting {settingName} were ignored because they are specific to certain build configurations: {string.Join(", ", skippedSettings)}");

        return result;
    }

    public bool IsEmpty => Values.Count == 0;
}
