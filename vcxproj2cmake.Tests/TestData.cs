using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace vcxproj2cmake.Tests;

internal static class TestData
{
    public static string DefaultEmptyProject => """
        <?xml version="1.0" encoding="utf-8"?>            
        <Project DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
            <ItemGroup Label="ProjectConfigurations">
            <ProjectConfiguration Include="Debug|Win32">
                <Configuration>Debug</Configuration>
                <Platform>Win32</Platform>
            </ProjectConfiguration>
            <ProjectConfiguration Include="Release|Win32">
                <Configuration>Release</Configuration>
                <Platform>Win32</Platform>
            </ProjectConfiguration>
            </ItemGroup>
            <PropertyGroup Label="Globals">
                <VCProjectVersion>17.0</VCProjectVersion>
                <Keyword>Win32Proj</Keyword>
                <ProjectGuid>{620c346a-996a-4c2b-8485-4a872433008b}</ProjectGuid>
                <RootNamespace>EmptyProject</RootNamespace>
                <WindowsTargetPlatformVersion>10.0</WindowsTargetPlatformVersion>
            </PropertyGroup>
            <Import Project="$(VCTargetsPath)\Microsoft.Cpp.Default.props" />
            <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Debug|Win32'" Label="Configuration">
                <ConfigurationType>Application</ConfigurationType>
                <UseDebugLibraries>true</UseDebugLibraries>
                <PlatformToolset>v143</PlatformToolset>
                <CharacterSet>Unicode</CharacterSet>
            </PropertyGroup>
            <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Release|Win32'" Label="Configuration">
                <ConfigurationType>Application</ConfigurationType>
                <UseDebugLibraries>false</UseDebugLibraries>
                <PlatformToolset>v143</PlatformToolset>
                <WholeProgramOptimization>true</WholeProgramOptimization>
                <CharacterSet>Unicode</CharacterSet>
            </PropertyGroup>
            <Import Project="$(VCTargetsPath)\Microsoft.Cpp.props" />
            <ImportGroup Label="ExtensionSettings">
            </ImportGroup>
            <ImportGroup Label="Shared">
            </ImportGroup>
            <ImportGroup Label="PropertySheets" Condition="'$(Configuration)|$(Platform)'=='Debug|Win32'">
            <Import Project="$(UserRootDir)\Microsoft.Cpp.$(Platform).user.props" Condition="exists('$(UserRootDir)\Microsoft.Cpp.$(Platform).user.props')" Label="LocalAppDataPlatform" />
            </ImportGroup>
            <ImportGroup Label="PropertySheets" Condition="'$(Configuration)|$(Platform)'=='Release|Win32'">
            <Import Project="$(UserRootDir)\Microsoft.Cpp.$(Platform).user.props" Condition="exists('$(UserRootDir)\Microsoft.Cpp.$(Platform).user.props')" Label="LocalAppDataPlatform" />
            </ImportGroup>
            <PropertyGroup Label="UserMacros" />
            <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Debug|Win32'">
                <ClCompile>
                    <WarningLevel>Level3</WarningLevel>
                    <SDLCheck>true</SDLCheck>
                    <PreprocessorDefinitions>WIN32;_DEBUG;_CONSOLE;%(PreprocessorDefinitions)</PreprocessorDefinitions>
                    <ConformanceMode>true</ConformanceMode>
                </ClCompile>
                <Link>
                    <SubSystem>Console</SubSystem>
                    <GenerateDebugInformation>true</GenerateDebugInformation>
                </Link>
            </ItemDefinitionGroup>
            <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Release|Win32'">
                <ClCompile>
                    <WarningLevel>Level3</WarningLevel>
                    <FunctionLevelLinking>true</FunctionLevelLinking>
                    <IntrinsicFunctions>true</IntrinsicFunctions>
                    <SDLCheck>true</SDLCheck>
                    <PreprocessorDefinitions>WIN32;NDEBUG;_CONSOLE;%(PreprocessorDefinitions)</PreprocessorDefinitions>
                    <ConformanceMode>true</ConformanceMode>
                </ClCompile>
                <Link>
                    <SubSystem>Console</SubSystem>
                    <GenerateDebugInformation>true</GenerateDebugInformation>
                </Link>
            </ItemDefinitionGroup>
            <Import Project="$(VCTargetsPath)\Microsoft.Cpp.targets" />
            <ImportGroup Label="ExtensionTargets">
            </ImportGroup>
        </Project>
        """;

    public static VcxProjectBuilder Project() => new();
}

internal class VcxProjectBuilder
{
    static readonly ProjectConfiguration[] DefaultConfigurations = [
        new("Debug", "Win32", UseDebugLibraries: true),
        new("Release", "Win32", UseDebugLibraries: false),
    ];

    readonly List<ProjectConfiguration> configurations = DefaultConfigurations.ToList();

    readonly List<string> imports = [];
    readonly List<PropertyGroupBuilder> propertyGroups = [];
    readonly List<ItemDefinitionGroupBuilder> itemDefinitionGroups = [];
    readonly List<ItemBuilder> items = [];
    readonly List<string> rawXml = [];

    public VcxProjectBuilder WithConfigurations(params IEnumerable<(string Configuration, string Platform)> projectConfigurations)
    {
        configurations.Clear();

        foreach (var (configuration, platform) in projectConfigurations)
            configurations.Add(new(configuration, platform, UseDebugLibraries: configuration == "Debug"));

        return this;
    }

    public VcxProjectBuilder WithProperty(string name, string value)
    {
        GetPropertyGroup(null, null).Add(name, value);
        return this;
    }

    public VcxProjectBuilder WithProperty(string configuration, string platform, string name, string value)
    {
        GetPropertyGroup(configuration, platform).Add(name, value);
        return this;
    }

    public VcxProjectBuilder WithImports(params IEnumerable<string> imports)
    {
        this.imports.AddRange(imports);
        return this;
    }

    public VcxProjectBuilder WithItemDefinitionSetting(string tool, string setting, string value)
    {
        GetItemDefinitionGroup(null, null).Add(tool, setting, value);
        return this;
    }

    public VcxProjectBuilder WithItemDefinitionSetting(string configuration, string platform, string tool, string setting, string value)
    {
        GetItemDefinitionGroup(configuration, platform).Add(tool, setting, value);
        return this;
    }

    public VcxProjectBuilder WithClCompileSetting(string setting, string value)
    {
        WithItemDefinitionSetting("ClCompile", setting, value);
        return this;
    }

    public VcxProjectBuilder WithClCompileSetting(string setting, string debugValue, string releaseValue)
    {
        if (!configurations.SequenceEqual(DefaultConfigurations))
            throw new InvalidOperationException("This method is only supported for the default configurations.");

        WithItemDefinitionSetting("Debug", "Win32", "ClCompile", setting, debugValue);
        WithItemDefinitionSetting("Release", "Win32", "ClCompile", setting, releaseValue);
        return this;
    }

    public VcxProjectBuilder WithLinkSetting(string setting, string value)
    {
        WithItemDefinitionSetting("Link", setting, value);
        return this;
    }

    public VcxProjectBuilder WithLinkSetting(string setting, string debugValue, string releaseValue)
    {
        if (!configurations.SequenceEqual(DefaultConfigurations))
            throw new InvalidOperationException("This method is only supported for the default configurations.");

        WithItemDefinitionSetting("Debug", "Win32", "Link", setting, debugValue);
        WithItemDefinitionSetting("Release", "Win32", "Link", setting, releaseValue);
        return this;
    }

    public VcxProjectBuilder WithItems(string itemType, params IEnumerable<string> items)
    {
        foreach (var item in items)
            this.items.Add(new(itemType, item));

        return this;
    }

    public VcxProjectBuilder WithProjectReferences(params IEnumerable<string> projectReferences)
    {
        WithItems("ProjectReference", projectReferences);
        return this;
    }

    public VcxProjectBuilder WithRawXml([StringSyntax(StringSyntaxAttribute.Xml)] string xml)
    {
        rawXml.Add(xml);
        return this;
    }

    public string Build()
    {
        var builder = new StringBuilder();
        builder.AppendLine("""<?xml version="1.0" encoding="utf-8"?>""");
        builder.AppendLine("""<Project DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">""");

        AppendProjectConfigurations();
        AppendImports();
        AppendConfigurationPropertyGroups();
        AppendCustomPropertyGroups();
        AppendItemDefinitionGroups();
        AppendItems();
        AppendRawXml();

        builder.AppendLine("</Project>");
        return builder.ToString();

        void AppendProjectConfigurations()
        {
            if (configurations.Count == 0)
                return;

            builder.AppendLine("""    <ItemGroup Label="ProjectConfigurations">""");
            foreach (var configuration in configurations)
            {
                builder.AppendLine($"""        <ProjectConfiguration Include="{configuration.Configuration}|{configuration.Platform}">""");
                builder.AppendLine($"""            <Configuration>{configuration.Configuration}</Configuration>""");
                builder.AppendLine($"""            <Platform>{configuration.Platform}</Platform>""");
                builder.AppendLine("""        </ProjectConfiguration>""");
            }

            builder.AppendLine("""    </ItemGroup>""");
        }

        void AppendImports()
        {
            foreach (var import in imports)
                builder.AppendLine($"""    <Import Project="{import}" />""");
        }

        void AppendConfigurationPropertyGroups()
        {
            foreach (var configuration in configurations)
            {
                builder.AppendLine($"""    <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='{configuration.Configuration}|{configuration.Platform}'" Label="Configuration">""");
                if (configuration.UseDebugLibraries != null)
                    builder.AppendLine($"""        <UseDebugLibraries>{(configuration.UseDebugLibraries.Value ? "true" : "false")}</UseDebugLibraries>""");

                var propertyGroup = FindPropertyGroup(configuration.Configuration, configuration.Platform);
                propertyGroup?.AppendContent(builder, "        ");
                builder.AppendLine("""    </PropertyGroup>""");
            }
        }

        void AppendCustomPropertyGroups()
        {
            foreach (var propertyGroup in propertyGroups.Where(group => group.Configuration == null))
            {
                builder.AppendLine("""    <PropertyGroup>""");
                propertyGroup.AppendContent(builder, "        ");
                builder.AppendLine("""    </PropertyGroup>""");
            }
        }

        void AppendItemDefinitionGroups()
        {
            foreach (var group in itemDefinitionGroups)
            {
                if (group.Configuration == null)
                    builder.AppendLine("""    <ItemDefinitionGroup>""");
                else
                    builder.AppendLine($"""    <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='{group.Configuration}|{group.Platform}'">""");

                group.AppendContent(builder, "        ");
                builder.AppendLine("""    </ItemDefinitionGroup>""");
            }
        }

        void AppendItems()
        {
            if (items.Count == 0)
                return;

            builder.AppendLine("""    <ItemGroup>""");
            foreach (var item in items)
            {
                builder.AppendLine($"""        <{item.ItemType} Include="{item.Item}" />""");
            }

            builder.AppendLine("""    </ItemGroup>""");
        }

        void AppendRawXml()
        {
            foreach (var xml in rawXml)
                VcxProjectBuilder.AppendRawXml(builder, xml, indent: "    ");
        }
    }

    PropertyGroupBuilder GetPropertyGroup(string? configuration, string? platform)
    {
        var existing = FindPropertyGroup(configuration, platform);
        if (existing != null)
            return existing;

        var propertyGroup = new PropertyGroupBuilder(configuration, platform);
        propertyGroups.Add(propertyGroup);
        return propertyGroup;
    }

    PropertyGroupBuilder? FindPropertyGroup(string? configuration, string? platform)
        => propertyGroups.FirstOrDefault(group => group.Configuration == configuration && group.Platform == platform);

    ItemDefinitionGroupBuilder GetItemDefinitionGroup(string? configuration, string? platform)
    {
        var existing = itemDefinitionGroups.FirstOrDefault(group => group.Configuration == configuration && group.Platform == platform);
        if (existing != null)
            return existing;

        var itemDefinitionGroup = new ItemDefinitionGroupBuilder(configuration, platform);
        itemDefinitionGroups.Add(itemDefinitionGroup);
        return itemDefinitionGroup;
    }

    static void AppendRawXml(StringBuilder builder, string xml, string indent)
    {
        var normalized = xml.ReplaceLineEndings("\n").Trim();
        foreach (var line in normalized.Split('\n'))
            builder.Append(indent).AppendLine(line.TrimEnd());
    }

    record ProjectConfiguration(string Configuration, string Platform, bool? UseDebugLibraries);

    record ItemBuilder(string ItemType, string Item);

    class PropertyGroupBuilder(string? configuration, string? platform)
    {
        readonly List<PropertyBuilder> properties = [];
        readonly List<string> rawXml = [];

        public string? Configuration { get; } = configuration;
        public string? Platform { get; } = platform;

        public void Add(string name, string value)
            => properties.Add(new(name, value));

        public void AddRaw(string xml)
            => rawXml.Add(xml);

        public void AppendContent(StringBuilder builder, string indent)
        {
            foreach (var property in properties)
                builder.AppendLine($"""{indent}<{property.Name}>{property.Value}</{property.Name}>""");

            foreach (var xml in rawXml)
                AppendRawXml(builder, xml, indent);
        }
    }

    class ItemDefinitionGroupBuilder(string? configuration, string? platform)
    {
        readonly List<ToolBuilder> tools = [];
        readonly List<string> rawXml = [];

        public string? Configuration { get; } = configuration;
        public string? Platform { get; } = platform;

        public void Add(string toolName, string property, string value)
        {
            var tool = tools.FirstOrDefault(tool => tool.Name == toolName);
            if (tool == null)
            {
                tool = new(toolName);
                tools.Add(tool);
            }

            tool.Properties.Add(new(property, value));
        }

        public void AddRaw(string xml)
            => rawXml.Add(xml);

        public void AppendContent(StringBuilder builder, string indent)
        {
            foreach (var tool in tools)
            {
                builder.AppendLine($"""{indent}<{tool.Name}>""");
                foreach (var property in tool.Properties)
                    builder.AppendLine($"""{indent}    <{property.Name}>{property.Value}</{property.Name}>""");

                builder.AppendLine($"""{indent}</{tool.Name}>""");
            }

            foreach (var xml in rawXml)
                AppendRawXml(builder, xml, indent);
        }
    }

    class ToolBuilder(string name)
    {
        public string Name { get; } = name;
        public List<PropertyBuilder> Properties { get; } = [];
    }

    record PropertyBuilder(string Name, string Value);
}
