# PayCalc24

PayCalc24 is a .NET 9 modular monolith for company-scoped, configuration-driven payroll.

The implementation follows specification pack v0.4. Localization, theme selection, Ribbon command metadata, and semantic icons are treated as platform architecture; their contracts begin in Task 02 and feature UI is implemented only in its scheduled tasks.

## Build

```bash
dotnet restore PayCalc24.sln
dotnet build PayCalc24.sln --no-restore
dotnet test PayCalc24.sln --no-build
```

The solution structure and dependency rules are documented in `docs/adr` and enforced by `PayCalc24.ArchitectureTests`.
