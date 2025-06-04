# vcxproj2cmake

**vcxproj2cmake** is a tool designed to convert Microsoft Visual C++ project files (`.vcxproj`) to equivalent CMake files (`CMakeLists.txt`).

> [!NOTE]
> Due to the complexity of MSBuild, the conversion works best for projects of low-to-medium complexity.
> The generated CMake files may require manual adjustments, especially for larger projects with complex configurations or custom build steps.
> Still, it can be a useful starting point for migrating projects to CMake and can save a lot of time compared to writing the CMake files from scratch.

## Features

* Accepts either a list of `.vcxproj` project files or a `.sln` solution file as input.
* Supports console, Win32, Dynamic-Link Library (DLL), and Static Library project types.
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

At the moment, no binary releases are available.
Therefore, you need to build the application from source:

1. Make sure you have .NET 9 SDK installed.
2. Run `cd vcxproj2cmake` and `dotnet run -- --help` to compile the project and see the usage instructions.

## Usage

### Basic Usage

#### Single Project

To convert a single `.vcxproj` file with no dependency to other projects, run:

```
cd vcxproj2cmake
dotnet run -- --projects MyProject.vcxproj
```

This will generate a `CMakeLists.txt` file in the same directory as the `.vcxproj` file.

#### Multiple Projects

If the project has dependencies on other projects, or you want to convert multiple projects at once,
specify the paths to all `.vcxproj` files:

```
cd vcxproj2cmake
dotnet run -- --projects MyProject1.vcxproj MyProject2.vcxproj
```

This will generate a `CMakeLists.txt` file for each specified project in their respective directories.

#### Solution File

If you have a `.sln` solution file, you can convert all projects in the solution by running:

```
cd vcxproj2cmake
dotnet run -- --solution MySolution.sln
```

This will generate a `CMakeLists.txt` file for each project next to the `.vcxproj` file,
as well as a top-level `CMakeLists.txt` file in the same directory as the `.sln` file.

### Customizing Output

* Specify the `--enable-standalone-project-builds` option to include additional CMake commands in the generated project `CMakeLists.txt` files
  to allow configuring the projects directly instead of as part of the top-level solution `CMakeLists.txt`.
* Specify the `--dry-run` option to have the generated CMake files printed to the console without writing them to disk.
* If any of your projects use Qt, you must specify the `--qt-version` option to indicate the Qt version (5 or 6) used in the project.

## Limitations

* vcxproj2cmake expects project configurations and build platforms to be named `Debug`/`Release` and `Win32`/`x86`/`x64`/`ARM32`/`ARM64`, respectively.
  Configurations and platforms with other names are ignored by default.
  If you would like to add support for your custom configurations/platforms, extend `Config.Configs` in [Config.cs](vcxproj2cmake/Config.cs).
* MSBuild properties whose value depends on the build configuration or platform are only supported
  if the value depends solely on the configuration or platform, but not both.
  E.g. preprocessor definitions like `_DEBUG` or `WIN32` are supported.
  They are converted to CMake generator expressions like `$<$<CONFIG:Debug>:_DEBUG>` or `$<$<STREQUAL:$<TARGET_PROPERTY:ARCHITECTURE_ID>,x86>:WIN32>`.
  A definition that is specific to a certain combination of configuration and platform, is not supported and skipped with a warning.
* MSBuild properties defined in imported .props or .targets files are not considered.
* Many advanced compiler and linker options are not supported and silently ignored.
  Only a limited set of commonly-used properties is converted, as listed in the [Features](#features) section.

## Contributing
Contributions of any kind (e.g. bug fixes, improvements or new features) are gladly accepted!
