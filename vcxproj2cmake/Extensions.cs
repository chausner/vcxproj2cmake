using Microsoft.Extensions.Logging;

namespace vcxproj2cmake;

static class ConfigDependentSettingExtensions
{
    extension(CMakeConfigDependentSetting self)
    {
        public CMakeConfigDependentSetting Map(
            Func<CMakeExpression?, CMakeExpression?> mapper,
            IEnumerable<MSBuildProjectConfig> projectConfigurations,
            ILogger logger)
        {
            Dictionary<MSBuildProjectConfig, CMakeExpression> mappedValues = [];

            foreach (var projectConfig in projectConfigurations)
            {
                var value = self.GetValue(projectConfig);
                var mappedValue = mapper(value);
                if (mappedValue != null)
                    mappedValues[projectConfig] = mappedValue;
            }

            var msbuildSetting = new MSBuildConfigDependentSetting<CMakeExpression>(self.SettingName, self.DefaultValue, mappedValues);

            return new(msbuildSetting, projectConfigurations, logger);
        }

        public CMakeConfigDependentSetting Map(
            Func<CMakeExpression?, CMakeExpression?, CMakeExpression?> mapper,
            CMakeConfigDependentSetting setting,
            IEnumerable<MSBuildProjectConfig> projectConfigurations,
            ILogger logger)
        {
            Dictionary<MSBuildProjectConfig, CMakeExpression> mappedValues = [];

            foreach (var projectConfig in projectConfigurations)
            {
                var value1 = self.GetValue(projectConfig);
                var value2 = setting.GetValue(projectConfig);
                var mappedValue = mapper(value1, value2);
                if (mappedValue != null)
                    mappedValues[projectConfig] = mappedValue;
            }

            var msbuildSetting = new MSBuildConfigDependentSetting<CMakeExpression>(self.SettingName, self.DefaultValue, mappedValues);

            return new(msbuildSetting, projectConfigurations, logger);
        }

        public CMakeConfigDependentSetting Map(
            Func<CMakeExpression?, CMakeExpression?, CMakeExpression?> mapper,
            MSBuildConfigDependentSetting<CMakeExpression> setting,
            IEnumerable<MSBuildProjectConfig> projectConfigurations,
            ILogger logger)
        {
            return self.Map(mapper, new CMakeConfigDependentSetting(setting, projectConfigurations, logger), projectConfigurations, logger);
        }

        public CMakeConfigDependentSetting Map(
            Func<CMakeExpression?, CMakeExpression[], CMakeExpression?> mapper,
            CMakeConfigDependentMultiSetting setting,
            IEnumerable<MSBuildProjectConfig> projectConfigurations,
            ILogger logger)
        {
            Dictionary<MSBuildProjectConfig, CMakeExpression> mappedValues = [];

            foreach (var projectConfig in projectConfigurations)
            {
                var value1 = self.GetValue(projectConfig);
                var value2 = setting.GetValue(projectConfig);
                var mappedValue = mapper(value1, value2);
                if (mappedValue != null)
                    mappedValues[projectConfig] = mappedValue;
            }

            var msbuildSetting = new MSBuildConfigDependentSetting<CMakeExpression>(self.SettingName, self.DefaultValue, mappedValues);

            return new(msbuildSetting, projectConfigurations, logger);
        }

        public CMakeConfigDependentSetting Map(
            Func<CMakeExpression?, CMakeExpression[], CMakeExpression?> mapper,
            MSBuildConfigDependentSetting<CMakeExpression[]> setting,
            IEnumerable<MSBuildProjectConfig> projectConfigurations,
            ILogger logger)
        {
            return self.Map(mapper, new CMakeConfigDependentMultiSetting(setting, projectConfigurations, logger), projectConfigurations, logger);
        }

        public CMakeConfigDependentSetting Map(
            Func<CMakeExpression?, CMakeExpression?, CMakeExpression?> mapper,
            MSBuildConfigDependentSetting<string> setting,
            IEnumerable<MSBuildProjectConfig> projectConfigurations,
            ILogger logger)
        {
            return self.Map(mapper, new CMakeConfigDependentSetting(setting, projectConfigurations, logger), projectConfigurations, logger);
        }

        public CMakeConfigDependentSetting Map(
            Func<CMakeExpression?, CMakeExpression[], CMakeExpression?> mapper,
            MSBuildConfigDependentSetting<string[]> setting,
            IEnumerable<MSBuildProjectConfig> projectConfigurations,
            ILogger logger)
        {
            return self.Map(mapper, new CMakeConfigDependentMultiSetting(setting, projectConfigurations, logger), projectConfigurations, logger);
        }
    }

    extension(CMakeConfigDependentMultiSetting self)
    {
        public CMakeConfigDependentMultiSetting Map(
            Func<CMakeExpression[], CMakeExpression[]> mapper,
            IEnumerable<MSBuildProjectConfig> projectConfigurations,
            ILogger logger)
        {
            Dictionary<MSBuildProjectConfig, CMakeExpression[]> mappedValues = [];

            foreach (var projectConfig in projectConfigurations)
            {
                var value = self.GetValue(projectConfig);
                var mappedValue = mapper(value);
                if (mappedValue != null)
                    mappedValues[projectConfig] = mappedValue;
            }

            var msbuildSetting = new MSBuildConfigDependentSetting<CMakeExpression[]>(self.SettingName, self.DefaultValue, mappedValues);

            return new(msbuildSetting, projectConfigurations, logger);
        }

        public CMakeConfigDependentMultiSetting Map(
            Func<CMakeExpression[], CMakeExpression?, CMakeExpression[]> mapper,
            CMakeConfigDependentSetting setting,
            IEnumerable<MSBuildProjectConfig> projectConfigurations,
            ILogger logger)
        {
            Dictionary<MSBuildProjectConfig, CMakeExpression[]> mappedValues = [];

            foreach (var projectConfig in projectConfigurations)
            {
                var value1 = self.GetValue(projectConfig);
                var value2 = setting.GetValue(projectConfig);
                var mappedValue = mapper(value1, value2);
                if (mappedValue != null)
                    mappedValues[projectConfig] = mappedValue;
            }

            var msbuildSetting = new MSBuildConfigDependentSetting<CMakeExpression[]>(self.SettingName, self.DefaultValue, mappedValues);

            return new(msbuildSetting, projectConfigurations, logger);
        }

        public CMakeConfigDependentMultiSetting Map(
            Func<CMakeExpression[], CMakeExpression?, CMakeExpression[]> mapper,
            MSBuildConfigDependentSetting<CMakeExpression> setting,
            IEnumerable<MSBuildProjectConfig> projectConfigurations,
            ILogger logger)
        {
            return self.Map(mapper, new CMakeConfigDependentSetting(setting, projectConfigurations, logger), projectConfigurations, logger);
        }

        public CMakeConfigDependentMultiSetting Map(
            Func<CMakeExpression[], CMakeExpression?, CMakeExpression[]> mapper,
            MSBuildConfigDependentSetting<string> setting,
            IEnumerable<MSBuildProjectConfig> projectConfigurations,
            ILogger logger)
        {
            return self.Map(mapper, new CMakeConfigDependentSetting(setting, projectConfigurations, logger), projectConfigurations, logger);
        }

        public CMakeConfigDependentMultiSetting Map(
            Func<CMakeExpression[], CMakeExpression[], CMakeExpression[]> mapper,
            MSBuildConfigDependentSetting<string[]> setting,
            IEnumerable<MSBuildProjectConfig> projectConfigurations,
            ILogger logger)
        {
            return self.Map(mapper, new CMakeConfigDependentMultiSetting(setting, projectConfigurations, logger), projectConfigurations, logger);
        }

        public CMakeConfigDependentMultiSetting Map(
            Func<CMakeExpression[], CMakeExpression[], CMakeExpression[]> mapper,
            CMakeConfigDependentMultiSetting setting,
            IEnumerable<MSBuildProjectConfig> projectConfigurations,
            ILogger logger)
        {
            Dictionary<MSBuildProjectConfig, CMakeExpression[]> mappedValues = [];

            foreach (var projectConfig in projectConfigurations)
            {
                var value1 = self.GetValue(projectConfig);
                var value2 = setting.GetValue(projectConfig);
                var mappedValue = mapper(value1, value2);
                if (mappedValue != null)
                    mappedValues[projectConfig] = mappedValue;
            }

            var msbuildSetting = new MSBuildConfigDependentSetting<CMakeExpression[]>(self.SettingName, self.DefaultValue, mappedValues);

            return new(msbuildSetting, projectConfigurations, logger);
        }

        public CMakeConfigDependentMultiSetting Map(
            Func<CMakeExpression[], CMakeExpression[], CMakeExpression[]> mapper,
            MSBuildConfigDependentSetting<CMakeExpression[]> setting,
            IEnumerable<MSBuildProjectConfig> projectConfigurations,
            ILogger logger)
        {
            return self.Map(mapper, new CMakeConfigDependentMultiSetting(setting, projectConfigurations, logger), projectConfigurations, logger);
        }
    }
}

static class EnumerableExtensions
{
    extension<TSource>(IEnumerable<TSource> source)
    {
        public TSource SingleWithException(Func<Exception> exception)
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

        public TSource SingleOrDefaultWithException(TSource defaultValue, Func<Exception> exception)
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

        public Dictionary<TKey, TValue> ToDictionaryKeepingLast<TKey, TValue>(Func<TSource, TKey> keySelector, Func<TSource, TValue> valueSelector)
            where TKey : notnull
        {
            Dictionary<TKey, TValue> dictionary = [];

            foreach (var item in source)
                dictionary[keySelector(item)] = valueSelector(item);

            return dictionary;
        }
    }
}

static class TextWriterExtensions
{
    const string DefaultForegroundColor = "\e[39m\e[22m";
    const string DefaultBackgroundColor = "\e[49m";

    extension(TextWriter textWriter)
    {
        public void WriteColored(string message, ConsoleColor? background, ConsoleColor? foreground)
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

        public void WriteLineColored(string message, ConsoleColor? background, ConsoleColor? foreground)
        {
            WriteColored(textWriter, message + Environment.NewLine, background, foreground);
        }
    }

    static string GetForegroundColorEscapeCode(ConsoleColor color) =>
        color switch
        {
            ConsoleColor.Black => "\e[30m",
            ConsoleColor.DarkRed => "\e[31m",
            ConsoleColor.DarkGreen => "\e[32m",
            ConsoleColor.DarkYellow => "\e[33m",
            ConsoleColor.DarkBlue => "\e[34m",
            ConsoleColor.DarkMagenta => "\e[35m",
            ConsoleColor.DarkCyan => "\e[36m",
            ConsoleColor.Gray => "\e[37m",
            ConsoleColor.Red => "\e[1m\e[31m",
            ConsoleColor.Green => "\e[1m\e[32m",
            ConsoleColor.Yellow => "\e[1m\e[33m",
            ConsoleColor.Blue => "\e[1m\e[34m",
            ConsoleColor.Magenta => "\e[1m\e[35m",
            ConsoleColor.Cyan => "\e[1m\e[36m",
            ConsoleColor.White => "\e[1m\e[37m",
            _ => DefaultForegroundColor
        };

    static string GetBackgroundColorEscapeCode(ConsoleColor color) =>
        color switch
        {
            ConsoleColor.Black => "\e[40m",
            ConsoleColor.DarkRed => "\e[41m",
            ConsoleColor.DarkGreen => "\e[42m",
            ConsoleColor.DarkYellow => "\e[43m",
            ConsoleColor.DarkBlue => "\e[44m",
            ConsoleColor.DarkMagenta => "\e[45m",
            ConsoleColor.DarkCyan => "\e[46m",
            ConsoleColor.Gray => "\e[47m",
            _ => DefaultBackgroundColor
        };
}