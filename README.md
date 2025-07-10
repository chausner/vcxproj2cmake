# vcxproj2cmake

[![GitHub release (latest by date)](https://img.shields.io/github/v/release/chausner/vcxproj2cmake)](https://github.com/chausner/vcxproj2cmake/releases/latest)
[![license](https://img.shields.io/github/license/chausner/vcxproj2cmake.svg)](https://github.com/chausner/vcxproj2cmake/blob/master/LICENSE)

**vcxproj2cmake** is a tool designed to convert Microsoft Visual C++ project files (`.vcxproj`) to equivalent CMake files (`CMakeLists.txt`).

> [!NOTE]
> Due to the complexity of MSBuild, the conversion works best for projects of low-to-medium complexity.
> The generated CMake files may require manual adjustments, especially for larger projects with complex configurations or custom build steps.
> Still, it can be a useful starting point for migrating projects to CMake and can save a lot of time compared to writing the CMake files from scratch.

## Features

* Accepts either a list of `.vcxproj` project files or a `.sln` solution file as input.
* Supports console, Win32, Dynamic-Link Library (DLL), and Static Library project types.
  Includes detection of header-only libraries.
* Leverages CMake generator expressions for property values that are specific to certain build configurations (Debug, Release, Win32, x64).
* The following MSBuild project properties are converted/taken into account:

  `AdditionalDependencies`
  `AdditionalIncludeDirectories`
  `AdditionalLibraryDirectories`
  `AdditionalOptions`
  `AllProjectIncludesArePublic`
  `CharacterSet`
  `DisableSpecificWarnings`
  `ExternalWarningLevel`
  `LanguageStandard`
  `LanguageStandard_C`
  `LinkLibraryDependencies`
  `OpenMPSupport`
  `PrecompiledHeaderFile`
  `PreprocessorDefinitions`
  `PublicIncludeDirectories`
  `SubSystem`
  `TreatAngleIncludeAsExternal`
  `TreatSpecificWarningsAsErrors`
  `TreatWarningAsError`
  `WarningLevel`
* For projects referencing Qt 5 or 6 via Qt/MsBuild, a corresponding `find_package(Qt... REQUIRED COMPONENTS ...)` command is generated and `AUTOMOC`/`AUTOUIC`/`AUTORCC` are enabled.
* For projects referencing Conan packages via the MSBuildDeps generator, corresponding `find_package` commands are generated, intended to be used with the `CMakeDeps` generator.

## Installation

1. Make sure you have the [.NET 9 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/9.0) installed.
2. Download the latest release from the [releases page](https://github.com/chausner/vcxproj2cmake/releases) and unzip it to a directory of your choice.

## Usage

### Basic Usage

#### Single Project

To convert a single `.vcxproj` file with no dependency to other projects, run:

```
.\vcxproj2cmake --projects MyProject.vcxproj
```

This will generate a `CMakeLists.txt` file in the same directory as the `.vcxproj` file.

#### Multiple Projects

If the project has dependencies on other projects, or you want to convert multiple projects at once,
specify the paths to all `.vcxproj` files:

```
.\vcxproj2cmake --projects MyProject1.vcxproj MyProject2.vcxproj
```

This will generate a `CMakeLists.txt` file for each specified project in their respective directories.

#### Solution File

If you have a `.sln` solution file, you can convert all projects in the solution by running:

```
.\vcxproj2cmake --solution MySolution.sln
```

This will generate a `CMakeLists.txt` file for each project next to the `.vcxproj` file,
as well as a top-level `CMakeLists.txt` file in the same directory as the `.sln` file.

### Customizing Output

* Specify the `--enable-standalone-project-builds` option to include additional CMake commands in the generated project `CMakeLists.txt` files
  to allow configuring the projects directly instead of as part of the top-level solution `CMakeLists.txt`.
* If any of your projects use Qt, you must specify the `--qt-version` option to indicate the Qt version (5 or 6) used in the project.
* Use options `--indent-style` and `--indent-size` to customize the indentation in the generated CMake files.
* Specify the `--dry-run` option to have the generated CMake files printed to the console without writing them to disk.


## Example

The repository contains a small demo solution under `ExampleSolution`. It
consists of a static library project `MathLib` and an application project `App`
that depends on the library. The solution references both projects and the
application has a project reference to `MathLib`.

The top-level solution looks like this:

```text
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "MathLib", "MathLib\MathLib.vcxproj", "{4D944B1C-9EBF-4086-AE57-25DDEBF92F0D}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "App", "App\App.vcxproj", "{07DC28F8-AB37-42B2-A0C4-82D4766A9166}"
    ProjectSection(ProjectDependencies) = postProject
        {4D944B1C-9EBF-4086-AE57-25DDEBF92F0D} = {4D944B1C-9EBF-4086-AE57-25DDEBF92F0D}
    EndProjectSection
EndProject
```

`MathLib.vcxproj` includes a header and source file. It also demonstrates configuration-specific options:

```xml
<ItemGroup>
  <ClInclude Include="include\MathLib.h" />
  <ClCompile Include="MathLib.cpp" />
</ItemGroup>
<ItemDefinitionGroup>
  <ClCompile>
    <AdditionalIncludeDirectories>include;%(AdditionalIncludeDirectories)</AdditionalIncludeDirectories>
  </ClCompile>
</ItemDefinitionGroup>
<ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Debug|Win32'">
  <ClCompile>
    <PreprocessorDefinitions>DEBUG;%(PreprocessorDefinitions)</PreprocessorDefinitions>
    <LanguageStandard>stdcpp17</LanguageStandard>
  </ClCompile>
</ItemDefinitionGroup>
<ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Release|Win32'">
  <ClCompile>
    <PreprocessorDefinitions>NDEBUG;%(PreprocessorDefinitions)</PreprocessorDefinitions>
    <LanguageStandard>stdcpp17</LanguageStandard>
  </ClCompile>
</ItemDefinitionGroup>
```

`App.vcxproj` references the library project, adds its include directory and sets
similar configuration-specific defines and compiler options:

```xml
<ItemDefinitionGroup>
  <ClCompile>
    <AdditionalIncludeDirectories>..\MathLib\include;%(AdditionalIncludeDirectories)</AdditionalIncludeDirectories>
  </ClCompile>
</ItemDefinitionGroup>
<ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Debug|Win32'">
  <ClCompile>
    <PreprocessorDefinitions>MATHLIB;DEBUG;%(PreprocessorDefinitions)</PreprocessorDefinitions>
    <LanguageStandard>stdcpp17</LanguageStandard>
    <WarningLevel>Level4</WarningLevel>
  </ClCompile>
</ItemDefinitionGroup>
<ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Release|Win32'">
  <ClCompile>
    <PreprocessorDefinitions>MATHLIB;NDEBUG;%(PreprocessorDefinitions)</PreprocessorDefinitions>
    <AdditionalOptions>/O2 %(AdditionalOptions)</AdditionalOptions>
    <LanguageStandard>stdcpp17</LanguageStandard>
  </ClCompile>
</ItemDefinitionGroup>
<ItemGroup>
  <ProjectReference Include="..\MathLib\MathLib.vcxproj" />
</ItemGroup>
```

Running the converter on the solution generates three `CMakeLists.txt` files:

```
> .\vcxproj2cmake --solution ../ExampleSolution/ExampleSolution.sln
Parsing ../ExampleSolution/ExampleSolution.sln
Parsing ../ExampleSolution/MathLib/MathLib.vcxproj
Parsing ../ExampleSolution/App/App.vcxproj
Generating ../ExampleSolution/MathLib/CMakeLists.txt
Generating ../ExampleSolution/App/CMakeLists.txt
Generating ../ExampleSolution/CMakeLists.txt
```

`ExampleSolution/MathLib/CMakeLists.txt`:

```cmake
cmake_minimum_required(VERSION 3.13)
project(MathLib LANGUAGES CXX)

add_library(MathLib STATIC
    MathLib.cpp
)

target_compile_features(MathLib PUBLIC cxx_std_17)

target_include_directories(MathLib
    PUBLIC
        ${CMAKE_CURRENT_SOURCE_DIR}/include
)

target_compile_definitions(MathLib
    PUBLIC
        $<$<CONFIG:Debug>:DEBUG>
        $<$<CONFIG:Release>:NDEBUG>
)
```

`ExampleSolution/App/CMakeLists.txt`:

```cmake
cmake_minimum_required(VERSION 3.13)
project(App LANGUAGES CXX)

add_executable(App
    main.cpp
)

target_compile_features(App PUBLIC cxx_std_17)

target_include_directories(App
    PUBLIC
        ${CMAKE_CURRENT_SOURCE_DIR}/../MathLib/include
)

target_compile_definitions(App
    PUBLIC
        MATHLIB
        $<$<CONFIG:Debug>:DEBUG>
        $<$<CONFIG:Release>:NDEBUG>
)

target_link_libraries(App
    PUBLIC
        MathLib
)

target_compile_options(App
    PUBLIC
        $<$<CONFIG:Debug>:/W4>
        $<$<CONFIG:Release>:/O2>
)
```

`ExampleSolution/CMakeLists.txt`:

```cmake
cmake_minimum_required(VERSION 3.13)
project(ExampleSolution)

add_subdirectory(MathLib)
add_subdirectory(App)
```

## Limitations

* vcxproj2cmake expects project configurations and build platforms to be named `Debug`/`Release` and `Win32`/`x86`/`x64`/`ARM32`/`ARM64`, respectively.
  Configurations and platforms with other names are ignored by default.
  If you would like to add support for your custom configurations/platforms, extend `Config.Configs` in [Config.cs](vcxproj2cmake/Config.cs).
* MSBuild properties whose value depends on the build configuration or platform are only supported
  if the value depends solely on the configuration or platform, but not both.
  E.g. preprocessor definitions like `_DEBUG` or `WIN32` are supported.
  They are converted to CMake generator expressions like `$<$<CONFIG:Debug>:_DEBUG>` or `$<$<STREQUAL:$<CMAKE_CXX_COMPILER_ARCHITECTURE_ID>,x86>:WIN32>`.
  A definition that is specific to a certain combination of configuration and platform, is not supported and skipped with a warning.
* MSBuild properties defined in imported .props or .targets files are not considered.
* Many advanced compiler and linker options are not supported and silently ignored.
  Only a limited set of commonly-used properties is converted, as listed in the [Features](#features) section.

## Contributing
Contributions of any kind (e.g. bug fixes, improvements or new features) are gladly accepted!
