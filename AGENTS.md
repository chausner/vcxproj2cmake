# Repository Guidelines

## Project Structure & Module Organization
- `vcxproj2cmake/` — .NET 10 CLI source. Templates in `vcxproj2cmake/Resources/Templates/*.scriban`; Conan data in `vcxproj2cmake/Resources/conan-packages.csv`.
- `vcxproj2cmake.Tests/` — xUnit tests (unit and behavior). Subfolders mirror source (e.g., `ConverterTests/`).
- `ExampleSolution/` — small demo solution for manual testing.
- `Scripts/` — maintenance scripts (e.g., `GetConanPackageInfo.ps1`).
- `.github/workflows/dotnet.yml` — CI for build, test, and Windows publish.

## Build, Test, and Development Commands
- Restore: `dotnet restore`
- Build: `dotnet build -c Release`
- Test: `dotnet test -c Release`
- Run locally (example):
  - `dotnet run --project vcxproj2cmake -- --solution ExampleSolution/ExampleSolution.sln`
  - Use `--projects <path1> <path2>` for multiple `.vcxproj` files.
- Publish (Windows artifact): `dotnet publish vcxproj2cmake/vcxproj2cmake.csproj -c Release -r win-x64`
- Preview output without writing files: add `--dry-run`.

## Architecture Overview
- Entry point: `vcxproj2cmake/Program.cs` parses CLI options via `System.CommandLine`, configures logging, and invokes `Converter`.
- Models: MSBuild inputs are represented by `MSBuildSolution`, `MSBuildProject`, and `MSBuildProjectConfig`; CMake outputs by `CMakeSolution` and `CMakeProject`.
- Generation: `CMakeGenerator` renders Scriban templates from `vcxproj2cmake/Resources/Templates/*.scriban`.
- Metadata: `QtModuleInfoRepository` and `ConanPackageInfoRepository` provide `find_package(...)` hints (the latter sourced from `Resources/conan-packages.csv`).
- Utilities: `ProjectDependencyUtils`, `PathUtils`, `Extensions` support path and dependency handling; `CustomConsoleFormatter` improves log output; domain errors live in `Exceptions.cs`.
- Flow: `--solution`/`--projects` → parse MSBuild → build CMake model → render templates → write files or `--dry-run` to console.

## Coding Style & Naming Conventions
- C# with 4-space indentation; `nullable` and implicit usings are enabled.
- Names: `PascalCase` for types/methods/properties; `camelCase` for locals/parameters; interfaces prefix `I`.
- File names match primary type (e.g., `CMakeProject.cs`); tests end with `*Tests.cs`.
- Prefer small, focused classes; early returns; clear logging via `Microsoft.Extensions.Logging`.
- Use `System.IO.Abstractions` wrappers for filesystem access.

## Testing Guidelines
- Framework: xUnit (`[Fact]`, `[Theory]`).
- Location: under `vcxproj2cmake.Tests/`; mirror source folders when useful.
- Naming: `Given_<Arrange>_When_<Act>_Then_<Assert>`.
- Run: `dotnet test -c Release`.
- Keep tests deterministic; use `MockFileSystem` from IO.Abstractions for file I/O.

## Commit & Pull Request Guidelines
- Commit subjects in imperative mood; keep concise; include scope when helpful. Example: `Fix generator expressions for target architecture detection`.
- Link issues/PRs (e.g., `#42`) and describe rationale and behavior changes.
- PRs should include: summary, repro/usage examples, before/after notes, and tests for new behavior.
- Ensure CI passes on Windows and Linux: `dotnet build`, `dotnet test` must succeed.

## Security & Configuration Tips
- Avoid destructive changes; verify generated output first with `--dry-run`.
- `Scripts/GetConanPackageInfo.ps1` requires network access; use only to update `Resources/conan-packages.csv` (not part of regular build).
