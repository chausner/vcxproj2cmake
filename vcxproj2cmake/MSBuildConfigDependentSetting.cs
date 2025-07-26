namespace vcxproj2cmake;

class MSBuildConfigDependentSetting<TValue>
{
    public string SettingName { get; }
    public TValue DefaultValue { get; }
    public Dictionary<MSBuildProjectConfig, TValue> Values { get; }

    public MSBuildConfigDependentSetting(string settingName, TValue defaultValue, Dictionary<MSBuildProjectConfig, TValue> values)
    {
        SettingName = settingName;
        DefaultValue = defaultValue;
        Values = values;
    }

    public MSBuildConfigDependentSetting(string settingName, TValue defaultValue, Dictionary<MSBuildProjectConfig, string> settings, Func<string, TValue> parser)
    {
        SettingName = settingName;
        DefaultValue = defaultValue;
        Values = settings.ToDictionary(kvp => kvp.Key, kvp => parser(kvp.Value));
    }
}

record MSBuildProjectConfig(string Name)
{
    public override string ToString() => Name;
}
