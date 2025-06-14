using Microsoft.Extensions.Logging;

namespace vcxproj2cmake;

static class ConfigDependentSettingExtensions
{
    public static CMakeConfigDependentSetting Map(this CMakeConfigDependentSetting self, Func<string?, string?> mapper, IEnumerable<string> projectConfigurations, ILogger logger)
    {
        Dictionary<string, string> mappedValues = [];

        foreach (var projectConfig in projectConfigurations)
        {
            var value = self.GetValue(projectConfig);
            var mappedValue = mapper(value);
            if (mappedValue != null)            
                mappedValues[projectConfig] = mappedValue;            
        }

        var msbuildSetting = new MSBuildConfigDependentSetting<string>(self.SettingName, self.DefaultValue, mappedValues);

        return new(msbuildSetting, projectConfigurations, logger);
    }

    public static CMakeConfigDependentSetting Map(this CMakeConfigDependentSetting self, Func<string?, string?, string?> mapper, CMakeConfigDependentSetting setting, IEnumerable<string> projectConfigurations, ILogger logger)
    {
        Dictionary<string, string> mappedValues = [];

        foreach (var projectConfig in projectConfigurations)
        {
            var value1 = self.GetValue(projectConfig);
            var value2 = setting.GetValue(projectConfig);
            var mappedValue = mapper(value1, value2);
            if (mappedValue != null)
                mappedValues[projectConfig] = mappedValue;
        }

        var msbuildSetting = new MSBuildConfigDependentSetting<string>(self.SettingName, self.DefaultValue, mappedValues);

        return new(msbuildSetting, projectConfigurations, logger);
    }

    public static CMakeConfigDependentSetting Map(this CMakeConfigDependentSetting self, Func<string?, string?, string?> mapper, MSBuildConfigDependentSetting<string> setting, IEnumerable<string> projectConfigurations, ILogger logger)
    {
        return self.Map(mapper, new CMakeConfigDependentSetting(setting, projectConfigurations, logger), projectConfigurations, logger);
    }

    public static CMakeConfigDependentSetting Map(this CMakeConfigDependentSetting self, Func<string?, string[], string?> mapper, CMakeConfigDependentMultiSetting setting, IEnumerable<string> projectConfigurations, ILogger logger)
    {
        Dictionary<string, string> mappedValues = [];

        foreach (var projectConfig in projectConfigurations)
        {
            var value1 = self.GetValue(projectConfig);
            var value2 = setting.GetValue(projectConfig);
            var mappedValue = mapper(value1, value2);
            if (mappedValue != null)
                mappedValues[projectConfig] = mappedValue;
        }

        var msbuildSetting = new MSBuildConfigDependentSetting<string>(self.SettingName, self.DefaultValue, mappedValues);

        return new(msbuildSetting, projectConfigurations, logger);
    }

    public static CMakeConfigDependentSetting Map(this CMakeConfigDependentSetting self, Func<string?, string[], string?> mapper, MSBuildConfigDependentSetting<string[]> setting, IEnumerable<string> projectConfigurations, ILogger logger)
    { 
        return self.Map(mapper, new CMakeConfigDependentMultiSetting(setting, projectConfigurations, logger), projectConfigurations, logger);
    }

    public static CMakeConfigDependentMultiSetting Map(this CMakeConfigDependentMultiSetting self, Func<string[], string[]> mapper, IEnumerable<string> projectConfigurations, ILogger logger)
    {
        Dictionary<string, string[]> mappedValues = [];

        foreach (var projectConfig in projectConfigurations)
        {
            var value = self.GetValue(projectConfig);
            var mappedValue = mapper(value);
            if (mappedValue != null)
                mappedValues[projectConfig] = mappedValue;
        }

        var msbuildSetting = new MSBuildConfigDependentSetting<string[]>(self.SettingName, self.DefaultValue, mappedValues);

        return new(msbuildSetting, projectConfigurations, logger);
    }

    public static CMakeConfigDependentMultiSetting Map(this CMakeConfigDependentMultiSetting self, Func<string[], string?, string[]> mapper, CMakeConfigDependentSetting setting, IEnumerable<string> projectConfigurations, ILogger logger)
    {
        Dictionary<string, string[]> mappedValues = [];

        foreach (var projectConfig in projectConfigurations)
        {
            var value1 = self.GetValue(projectConfig);
            var value2 = setting.GetValue(projectConfig);
            var mappedValue = mapper(value1, value2);
            if (mappedValue != null)
                mappedValues[projectConfig] = mappedValue;
        }

        var msbuildSetting = new MSBuildConfigDependentSetting<string[]>(self.SettingName, self.DefaultValue, mappedValues);

        return new(msbuildSetting, projectConfigurations, logger);
    }

    public static CMakeConfigDependentMultiSetting Map(this CMakeConfigDependentMultiSetting self, Func<string[], string?, string[]> mapper, MSBuildConfigDependentSetting<string> setting, IEnumerable<string> projectConfigurations, ILogger logger)
    {
        return self.Map(mapper, new CMakeConfigDependentSetting(setting, projectConfigurations, logger), projectConfigurations, logger);
    }

    public static CMakeConfigDependentMultiSetting Map(this CMakeConfigDependentMultiSetting self, Func<string[], string[], string[]> mapper, CMakeConfigDependentMultiSetting setting, IEnumerable<string> projectConfigurations, ILogger logger)
    {
        Dictionary<string, string[]> mappedValues = [];

        foreach (var projectConfig in projectConfigurations)
        {
            var value1 = self.GetValue(projectConfig);
            var value2 = setting.GetValue(projectConfig);
            var mappedValue = mapper(value1, value2);
            if (mappedValue != null)
                mappedValues[projectConfig] = mappedValue;
        }

        var msbuildSetting = new MSBuildConfigDependentSetting<string[]>(self.SettingName, self.DefaultValue, mappedValues);

        return new(msbuildSetting, projectConfigurations, logger);
    }

    public static CMakeConfigDependentMultiSetting Map(this CMakeConfigDependentMultiSetting self, Func<string[], string[], string[]> mapper, MSBuildConfigDependentSetting<string[]> setting, IEnumerable<string> projectConfigurations, ILogger logger)
    {
        return self.Map(mapper, new CMakeConfigDependentMultiSetting(setting, projectConfigurations, logger), projectConfigurations, logger);
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

    public static OrderedDictionary<TKey, TValue> ToOrderedDictionary<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> source) where TKey : notnull
    {
        OrderedDictionary<TKey, TValue> dictionary = [];

        foreach (var kvp in source)
            dictionary[kvp.Key] = kvp.Value;

        return dictionary;
    }
}

static class TextWriterExtensions
{
    const string DefaultForegroundColor = "\x1B[39m\x1B[22m";
    const string DefaultBackgroundColor = "\x1B[49m";

    public static void WriteColored(this TextWriter textWriter, string message, ConsoleColor? background, ConsoleColor? foreground)
    {
        var backgroundColor = background.HasValue ? GetBackgroundColorEscapeCode(background.Value) : null;
        var foregroundColor = foreground.HasValue ? GetForegroundColorEscapeCode(foreground.Value) : null;

        if (backgroundColor != null)
            textWriter.Write(backgroundColor);

        if (foregroundColor != null)
            textWriter.Write(foregroundColor);

        textWriter.Write(message);

        if (foregroundColor != null)
            textWriter.Write(DefaultForegroundColor);
        if (backgroundColor != null)
            textWriter.Write(DefaultBackgroundColor);
    }

    public static void WriteLineColored(this TextWriter textWriter, string message, ConsoleColor? background, ConsoleColor? foreground)
    {
        WriteColored(textWriter, message + Environment.NewLine, background, foreground);
    }

    static string GetForegroundColorEscapeCode(ConsoleColor color) =>
        color switch
        {
            ConsoleColor.Black => "\x1B[30m",
            ConsoleColor.DarkRed => "\x1B[31m",
            ConsoleColor.DarkGreen => "\x1B[32m",
            ConsoleColor.DarkYellow => "\x1B[33m",
            ConsoleColor.DarkBlue => "\x1B[34m",
            ConsoleColor.DarkMagenta => "\x1B[35m",
            ConsoleColor.DarkCyan => "\x1B[36m",
            ConsoleColor.Gray => "\x1B[37m",
            ConsoleColor.Red => "\x1B[1m\x1B[31m",
            ConsoleColor.Green => "\x1B[1m\x1B[32m",
            ConsoleColor.Yellow => "\x1B[1m\x1B[33m",
            ConsoleColor.Blue => "\x1B[1m\x1B[34m",
            ConsoleColor.Magenta => "\x1B[1m\x1B[35m",
            ConsoleColor.Cyan => "\x1B[1m\x1B[36m",
            ConsoleColor.White => "\x1B[1m\x1B[37m",
            _ => DefaultForegroundColor
        };

    static string GetBackgroundColorEscapeCode(ConsoleColor color) =>
        color switch
        {
            ConsoleColor.Black => "\x1B[40m",
            ConsoleColor.DarkRed => "\x1B[41m",
            ConsoleColor.DarkGreen => "\x1B[42m",
            ConsoleColor.DarkYellow => "\x1B[43m",
            ConsoleColor.DarkBlue => "\x1B[44m",
            ConsoleColor.DarkMagenta => "\x1B[45m",
            ConsoleColor.DarkCyan => "\x1B[46m",
            ConsoleColor.Gray => "\x1B[47m",
            _ => DefaultBackgroundColor
        };
}