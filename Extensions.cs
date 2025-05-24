static class ConfigDependentSettingExtensions
{
    public static ConfigDependentSetting Map(this ConfigDependentSetting self, Func<string?, string?> mapper)
    {
        return new ConfigDependentSetting
        {
            Common = mapper(self.Common),
            Debug = mapper(self.Debug),
            Release = mapper(self.Release),
            X86 = mapper(self.X86),
            X64 = mapper(self.X64)
        };
    }

    public static ConfigDependentSetting Map(this ConfigDependentSetting self, Func<string?, string?, string?> mapper, ConfigDependentSetting setting)
    {
        return new ConfigDependentSetting
        {
            Common = mapper(self.Common, setting.Common),
            Debug = mapper(self.Debug, setting.Debug),
            Release = mapper(self.Release, setting.Release),
            X86 = mapper(self.X86, setting.X86),
            X64 = mapper(self.X64, setting.X64)
        };
    }

    public static ConfigDependentSetting Map(this ConfigDependentSetting self, Func<string?, string[], string?> mapper, ConfigDependentMultiSetting setting)
    {
        return new ConfigDependentSetting
        {
            Common = mapper(self.Common, setting.Common),
            Debug = mapper(self.Debug, setting.Debug),
            Release = mapper(self.Release, setting.Release),
            X86 = mapper(self.X86, setting.X86),
            X64 = mapper(self.X64, setting.X64)
        };
    }
    public static ConfigDependentMultiSetting Map(this ConfigDependentMultiSetting self, Func<string[], string[]> mapper)
    {
        return new ConfigDependentMultiSetting
        {
            Common = mapper(self.Common),
            Debug = mapper(self.Debug),
            Release = mapper(self.Release),
            X86 = mapper(self.X86),
            X64 = mapper(self.X64)
        };
    }

    public static ConfigDependentMultiSetting Map(this ConfigDependentMultiSetting self, Func<string[], string?, string[]> mapper, ConfigDependentSetting setting)
    {
        return new ConfigDependentMultiSetting
        {
            Common = mapper(self.Common, setting.Common),
            Debug = mapper(self.Debug, setting.Debug),
            Release = mapper(self.Release, setting.Release),
            X86 = mapper(self.X86, setting.X86),
            X64 = mapper(self.X64, setting.X64)
        };
    }

    public static ConfigDependentMultiSetting Map(this ConfigDependentMultiSetting self, Func<string[], string[], string[]> mapper, ConfigDependentMultiSetting setting)
    {
        return new ConfigDependentMultiSetting
        {
            Common = mapper(self.Common, setting.Common),
            Debug = mapper(self.Debug, setting.Debug),
            Release = mapper(self.Release, setting.Release),
            X86 = mapper(self.X86, setting.X86),
            X64 = mapper(self.X64, setting.X64)
        };
    }
}

static class EnumerableExtensions
{
    public static TSource SingleWithException<TSource>(this IEnumerable<TSource> source, Func<Exception> exception)
    {
        try
        {
            return source.Single();
        }
        catch (InvalidOperationException)
        {
            throw exception();
        }
    }

    public static TSource SingleOrDefaultWithException<TSource>(this IEnumerable<TSource> source, TSource defaultValue, Func<Exception> exception)
    {
        try
        {
            return source.SingleOrDefault(defaultValue);
        }
        catch (InvalidOperationException)
        {
            throw exception();
        }
    }
}