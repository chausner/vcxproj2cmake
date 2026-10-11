# Repository Guidelines

## Project Structure & Module Organization
- `vcxproj2cmake/` — .NET 10 CLI source. Scriban templates are embedded from `Resources/Templates/`; Conan package metadata is embedded from `Resources/conan-packages.csv`.
- `vcxproj2cmake.Tests/` — xUnit v3 tests, organized by area and conversion behavior (for example, `ConverterTests/`).
- `ExampleSolution/` — small `.slnx` demo with an app and a library, useful for manual conversion checks.
- `Scripts/` — maintenance scripts, including `GetConanPackageInfo.ps1`.
- `.github/workflows/dotnet.yml` — Windows and Ubuntu build/test CI, Windows publish and coverage upload, plus a Windows end-to-end job that converts real projects and configures/builds the generated CMake.
- `vcxproj2cmake.slnx` is the root solution. `global.json` selects Microsoft.Testing.Platform as the `dotnet test` runner.

## Build, Test, and Development Commands
Run these from the repository root:
- Restore: `dotnet restore`
- Build: `dotnet build --configuration Release`
- Test: `dotnet test --configuration Release`
- Run locally (example): `dotnet run --project vcxproj2cmake -- --solution ExampleSolution/ExampleSolution.slnx`
- Convert multiple projects with `--projects <path1> <path2>`; preview generated output without writing files by adding `--dry-run`.
- Publish: `dotnet publish vcxproj2cmake/vcxproj2cmake.csproj --configuration Release`. The project enables trimming and Native AOT; CI publishes the Windows executable.

## Architecture Overview
- Entry point: `vcxproj2cmake/Program.cs` defines the `System.CommandLine` options, configures logging, and invokes `Converter`.
- Input models: `MSBuildSolution`, `MSBuildProject`, and `MSBuildProjectConfig` read solution/project data and build settings.
- Output models and generation: `CMakeSolution` and `CMakeProject` prepare output; `CMakeGenerator` renders the embedded Scriban templates for project and solution `CMakeLists.txt` files.
- Configuration-dependent values are represented with `MSBuildConfigDependentSetting`, `CMakeConfigDependentSetting`, and `CMakeExpression` so MSBuild configuration/platform conditions can be translated into CMake expressions.
- Metadata: `QtModuleInfoRepository` maps Qt modules; `ConanPackageInfoRepository` reads the embedded package CSV. These provide `find_package(...)` information for generated output.
- `ProjectDependencyUtils`, `PathUtils`, and `Extensions` support dependency and path handling. `CustomConsoleFormatter` formats logs; domain errors live in `Exceptions.cs`.
- Flow: `--solution` or `--projects` → parse MSBuild → build the CMake model → render templates → write files or print them with `--dry-run`.

## Coding Style & Naming Conventions
- C# with 4-space indentation; nullable reference types and implicit usings are enabled.
- Names: `PascalCase` for types/methods/properties; `camelCase` for locals/parameters; interfaces prefix `I`.
- File names match primary types (for example, `CMakeProject.cs`); tests end with `*Tests.cs`.
- Prefer small, focused classes, early returns, and clear logging via `Microsoft.Extensions.Logging`.
- Use `System.IO.Abstractions` wrappers for production filesystem access.

## Testing Guidelines
- Tests use xUnit v3 with Microsoft.Testing.Platform. Keep them deterministic; use `MockFileSystem` from IO.Abstractions for filesystem behavior.
- Put tests under `vcxproj2cmake.Tests/`, grouped by area or conversion behavior. Name tests `Given_<Arrange>_When_<Act>_Then_<Assert>`.
- Mark test sections with `// Arrange`, `// Act`, and `// Assert` comments when useful; use `// Act & Assert` when the steps are intertwined. Leave out sections that do not apply and follow nearby tests.
- `CMakeAssert.ConfiguresAndBuildsWithCMake(...)` and `TestOptions.RunCMakeAssertions` are available for tests that must prove generated CMake configures and builds; there are currently no in-project tests using this helper. Gate any new calls with `TestOptions.RunCMakeAssertions` so ordinary runs do not require CMake. CI sets `RUN_CMAKE_ASSERTIONS=1`, and its separate Windows end-to-end job currently provides the configure/build integration checks against pinned real-world projects.
- `MockFileSystem.CopyCurrentDirectoryToDisk(...)` is for debugging generated test output. Add it only temporarily when needed and do not commit it.

## Commit & Pull Request Guidelines
- Commit subjects in imperative mood; keep them concise and include scope when helpful. Example: `Fix generator expressions for target architecture detection`.
- Link issues/PRs (for example, `#42`) and describe the rationale and behavior changes.
- PRs should include a summary, repro/usage examples, before/after notes, and tests for new behavior.
- CI builds and tests on Windows and Ubuntu; preserve that cross-platform coverage.

## Security & Configuration Tips
- Avoid destructive changes; verify generated output first with `--dry-run`.
- `Scripts/GetConanPackageInfo.ps1` requires network access; use it only to update `vcxproj2cmake/Resources/conan-packages.csv`, not as part of the regular build.
