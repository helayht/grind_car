# Repository Guidelines

## Project Structure & Module Organization
- `GrindCar.sln` is the solution entry point.
- `GrindCar/` is the WPF application project.
- UI: `GrindCar/Views/` (XAML + code-behind).
- ViewModels: `GrindCar/ViewModels/`.
- Models: `GrindCar/Models/`.
- Rail profile models: `GrindCar/Models/Rail/`.
- PLC/Modbus and business services: `GrindCar/Services/`.
- Rail-related services: `GrindCar/Services/Rail/`.
- Parameter/address definitions: `GrindCar/Definitions/`.
- Infrastructure helpers: `GrindCar/Infrastructure/`.
- Docs: `GrindCar/Doc/`.
- Build artifacts: `GrindCar/bin/`, `GrindCar/obj/` (generated).

## Build, Test, and Development Commands
- Build: `dotnet build GrindCar.sln`  
  Compiles the WPF app.
- Run: `dotnet run --project GrindCar/GrindCar.csproj`  
  Launches the UI locally.
- Clean: `dotnet clean GrindCar.sln`  
  Removes build outputs.

## Coding Style & Naming Conventions
- Language: C# (.NET 6, WPF).
- Indentation: 4 spaces in `.cs` and XAML.
- Namespaces follow folder structure (e.g., `GrindCar.ViewModels`, `GrindCar.Services`).
- Rail profile namespaces follow `GrindCar.Models.Rail` and `GrindCar.Services.Rail`.
- Classes: `PascalCase`; private fields: `_camelCase`.
- XAML resources and styles: `PascalCase` keys (e.g., `HeaderText`).
- Keep parameter names, Modbus addresses, scales, and units centralized in `Definitions/MotorParameterDefinitions.cs`.
- Keep write mapping logic centralized in `ViewModels/MotorViewModel.BuildWriteSpecs()`.
- Keep standard rail profile calculations in `Services/RailSurfaceService.cs`.
- Keep measured profile fitting logic in `Services/Rail/`.
- Do not mix UI concerns or file parsing into rail fitting services.

## Testing Guidelines
- No automated tests are currently present.
- If you add tests, prefer `xUnit` and place them under a new `GrindCar.Tests/` project.
- Suggested naming: `*Tests.cs` and test methods `MethodName_State_Expected`.

## Commit & Pull Request Guidelines
- Recent commit style uses `feat: <summary>` (e.g., `feat: 读写实现`).
- Keep commits small and scoped to a single change.
- PRs should include:
  - Summary of behavior changes.
  - Screenshots for UI changes.
  - PLC/Modbus impact notes if addressing addresses, scaling, or timing.

## Configuration & Safety Notes
- The repository uses `global.json`; ensure a compatible .NET 6 SDK is installed for command-line builds.
- Connection defaults are editable in the UI; no automatic PLC connection on startup.
- Validate IP/Port inputs before attempting Modbus connections.
- The dashboard home page currently contains presentation/demo data; PLC parameter read/write is handled in the motor debug window.
- Rail profile fitting currently outputs a callable function with a valid domain and does not perform extrapolation.
- Avoid scattering Modbus addresses across views or code-behind files.
- Avoid committing build artifacts under `GrindCar/bin/` and `GrindCar/obj/`.
- Current command-line build is clean except for `NU1701` warnings from `NModbus4 3.0.0-alpha1`.
