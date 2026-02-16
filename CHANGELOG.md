# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Fixed

- Fix incorrect version number reported by --version command-line option.
- Fix crash for projects with a lot (> 1000) of source files or property values.

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
- Add CLI option --continue-on-error.

### Fixed

- Improve handling of identifiers that require escaping in CMake.
- Fix translation of $(Configuration) MSBuild macro.
- Fix CMake generator expressions for target architecture detection.
- Fix some parts of the CLI help were not displayed in English, depending on the system language setting.

### Changed

- MSBuild property `TreatWarningAsError` is now converted to compiler-independent CMake target property `COMPILE_WARNING_AS_ERROR`.
- When LinkLibraryDependencies is enabled and a project references a header-only library project, no library is linked anymore.
- Improve warning message about unsupported MSBuild macros/properties.

## [1.0.0] - 2025-07-10

First stable release.

[unreleased]: https://github.com/chausner/vcxproj2cmake/compare/v1.2.0...HEAD
[1.2.0]: https://github.com/chausner/vcxproj2cmake/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/chausner/vcxproj2cmake/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/chausner/vcxproj2cmake/releases/tag/v1.0.0