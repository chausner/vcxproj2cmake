namespace vcxproj2cmake;

class MSBuildConfigDependentSetting<TValue>
{
    public string SettingName { get; }
    public TValue DefaultValue { get; }
    public Dictionary<string, TValue> Values { get; }

    public MSBuildConfigDependentSetting(string settingName, TValue defaultValue, Dictionary<string, TValue> values)
    {
        SettingName = settingName;
        DefaultValue = defaultValue;
        Values = values;
    }

    public MSBuildConfigDependentSetting(string settingName, TValue defaultValue, Dictionary<string, string> settings, Func<string, TValue> parser)
    {
        SettingName = settingName;
        DefaultValue = defaultValue;
        Values = settings.ToDictionary(kvp => kvp.Key, kvp => parser(kvp.Value));
    }

    public TValue GetEffectiveValue(string projectConfig) => Values.GetValueOrDefault(projectConfig, DefaultValue);
}
