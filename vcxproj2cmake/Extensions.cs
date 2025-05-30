namespace vcxproj2cmake;

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