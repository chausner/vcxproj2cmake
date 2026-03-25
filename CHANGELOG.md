# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added

- A warning is now logged when a CMake output file already exists and will be overwritten.

## [1.6.0] - 2026-03-24

### Added

- Console logs now include the currently processed file as context.

### Changed

- The application is now built self-contained with Native AOT.
- MSBuild imports related to Qt/MsBuild and Conan no longer generate warnings.
- Remove extra blank lines between `find_package()` calls in generated CMake files.
- Tweak wording in CLI help.
- Update list of Conan package metadata.

### Fixed

- When paths passed to options `--projects` or `--solution` are non-existent, a user-friendly error message is now displayed instead of an exception and stack trace.

## [1.5.0] - 2026-03-18

### Added

- Add support for linker property `ModuleDefinitionFile`.
- Log warnings when MSBuild imports (directly or indirectly via Directory.Build.props/targets) are ignored.
- Log warnings when file-specific MSBuild settings are ignored.

### Changed

- Remove extra blank line after `project()` CMake command in generated CMake files.

### Fixed

- Percent-escaped characters in MSBuild values are now properly handled.
- Values and expressions in CMake are now properly quoted when necessary.

## [1.4.0] - 2026-03-01

### Added

- Add support for MFC projects.
- Add support for Win32 .rc resource files.
- Support configuration-specific values for MSBuild property `TargetName`.

### Changed

- Use case-insensitive matching for MSBuild properties and macros to improve robustness and correctness.

### Fixed

- Fix option `--include-headers` not including header files processed with Qt MOC.

## [1.3.0] - 2026-02-22

### Added

- Add option `--include-headers` to include header files in the list of sources set via `target_sources` commands.
- Add support for MSBuild properties `IncludePath` and `LibraryPath`.

### Changed

- Generated CMake files now use the `target_sources` command instead of passing sources to `add_executable`/`add_library`.
- MSBuild macros in properties `PreprocessorDefinitions`, `AdditionalOptions` and `TargetName` are now translated.
- Update list of Conan package metadata.
- Improve warnings about unsupported MSBuild macros/properties.

### Fixed

- Fix incorrect version number reported by `--version` command-line option.
- Fix crash for projects with a lot (> 1000) of source files or property values.
- Some additional Qt5 and Qt6 modules are now supported that were previously not recognized.

## [1.2.0] - 2025-12-07

### Added

- Add support for .slnx solution format.

### Changed

- Retarget project to .NET 10.0.

### Fixed

- Fix incorrect handling of settings when project configurations are skipped.

## [1.1.0] - 2025-10-12

### Added

- The `TargetName` MSBuild property is now respected and converted to CMake target property `OUTPUT_NAME`.
- Allow ConfigurationType property to be unset and have its default value ("Application").
- Add support for `RuntimeLibrary` MSBuild property.
- Add CLI option `--continue-on-error`.

### Fixed

- Improve handling of identifiers that require escaping in CMake.
- Fix translation of `$(Configuration)` MSBuild macro.
- Fix CMake generator expressions for target architecture detection.
- Fix some parts of the CLI help were not displayed in English, depending on the system language setting.

### Changed

- MSBuild property `TreatWarningAsError` is now converted to compiler-independent CMake target property `COMPILE_WARNING_AS_ERROR`.
- When `LinkLibraryDependencies` is enabled and a project references a header-only library project, no library is linked anymore.
- Improve warning message about unsupported MSBuild macros/properties.

## [1.0.0] - 2025-07-10

First stable release.

[unreleased]: https://github.com/chausner/vcxproj2cmake/compare/v1.6.0...HEAD
[1.6.0]: https://github.com/chausner/vcxproj2cmake/compare/v1.5.0...v1.6.0
[1.5.0]: https://github.com/chausner/vcxproj2cmake/compare/v1.4.0...v1.5.0
[1.4.0]: https://github.com/chausner/vcxproj2cmake/compare/v1.3.0...v1.4.0
[1.3.0]: https://github.com/chausner/vcxproj2cmake/compare/v1.2.0...v1.3.0
[1.2.0]: https://github.com/chausner/vcxproj2cmake/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/chausner/vcxproj2cmake/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/chausner/vcxproj2cmake/releases/tag/v1.0.0
