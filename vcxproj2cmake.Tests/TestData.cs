using System.Text;

namespace vcxproj2cmake.Tests;

internal class TestData
{
    public static string EmptyProject => """
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

    public static string CreateProject(string configurationType = "Application", string? projectReference = null, string? targetName = null)
        => CreateProjectWithProjectReferences(
            configurationType: configurationType,
            targetName: targetName,
            projectReferences: projectReference is null ? [] : [projectReference]);

    public static string CreateProjectWithProjectReferences(string configurationType = "Application", string? targetName = null, params string[] projectReferences)
    {
        var references = new StringBuilder();
        if (projectReferences.Length > 0)
        {
            references.AppendLine("            <ItemGroup>");
            foreach (var projectReference in projectReferences)
                references.AppendLine($"                <ProjectReference Include=\"{projectReference}\" />");
            references.Append("            </ItemGroup>");
        }

        return $"""
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
                <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Debug|Win32'" Label="Configuration">
                    <UseDebugLibraries>true</UseDebugLibraries>
                </PropertyGroup>
                <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Release|Win32'" Label="Configuration">
                    <UseDebugLibraries>false</UseDebugLibraries>
                </PropertyGroup>
                <PropertyGroup>
                    <ConfigurationType>{configurationType}</ConfigurationType>
                {(targetName != null ? $"""
                    <TargetName>{targetName}</TargetName>
                """ : string.Empty)}
                </PropertyGroup>
                {references}
            </Project>
            """;
    }

    public static string CreateProjectWithClCompileProperty(string property, string debugValue, string releaseValue) => $"""
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
            <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Debug|Win32'" Label="Configuration">
                <UseDebugLibraries>true</UseDebugLibraries>
            </PropertyGroup>
            <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Release|Win32'" Label="Configuration">
                <UseDebugLibraries>false</UseDebugLibraries>
            </PropertyGroup>
            <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Debug|Win32'">
                <ClCompile>
                    <{property}>{debugValue}</{property}>
                </ClCompile>
            </ItemDefinitionGroup>
            <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Release|Win32'">
                <ClCompile>
                    <{property}>{releaseValue}</{property}>
                </ClCompile>
            </ItemDefinitionGroup>
        </Project>
        """;

    public static string CreateProjectWithSources(params string[] sources)
    {
        var clCompileItems = new StringBuilder();
        foreach (var source in sources)
            clCompileItems.AppendLine($"                <ClCompile Include=\"{source}\" />");

        var itemGroup = sources.Length == 0
            ? string.Empty
            : $"""
                <ItemGroup>
            {clCompileItems.ToString().TrimEnd()}
                </ItemGroup>
            """;

        return $"""
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
                <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Debug|Win32'" Label="Configuration">
                    <UseDebugLibraries>true</UseDebugLibraries>
                </PropertyGroup>
                <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Release|Win32'" Label="Configuration">
                    <UseDebugLibraries>false</UseDebugLibraries>
                </PropertyGroup>
                {itemGroup}
            </Project>
            """;
    }

    public static string CreateProjectWithItemGroups(string itemGroupsXml) => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <Project DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
            <ItemGroup Label="ProjectConfigurations">
                <ProjectConfiguration Include="Debug|Win32">
                    <Configuration>Debug</Configuration>
                    <Platform>Win32</Platform>
                </ProjectConfiguration>
            </ItemGroup>
            <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Debug|Win32'" Label="Configuration">
                <UseDebugLibraries>true</UseDebugLibraries>
            </PropertyGroup>
            {itemGroupsXml}
        </Project>
        """;

}
