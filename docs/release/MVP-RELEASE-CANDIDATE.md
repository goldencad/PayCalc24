# PayCalc24 MVP Release Candidate

- Version: `0.1.0-mvp`
- Original Task 20 baseline: `277ee64112516da36449c2bb84f346e99743205d`
- Build: `dotnet build PayCalc24.sln --configuration Release`
- Verification: 0 warnings, 0 errors; 190 tests passed (165 Application/Presentation/Reporting and 25 Architecture).
- Runtime: self-contained .NET 9 Avalonia desktop executable; signing and notarization are outside Task 20.
- Database: production requires configured MariaDB/application adapters. The desktop smoke path uses an explicit deterministic demo projection and does not claim database connectivity.
- Actipro: official `ActiproSoftware.Controls.Avalonia.Pro` `25.2.0` Trial/Evaluation with Avalonia `11.3.2`; real Ribbon and Backstage are integrated. Production uses the company's commercial license supplied through `PAYCALC24_ACTIPRO_LICENSEE` and `PAYCALC24_ACTIPRO_LICENSE_KEY`. Neither value is logged or stored in the repository.

## Supported test artifacts

### macOS ARM64

- RID: `osx-arm64`
- Command: `dotnet publish src/PayCalc24.Client.Avalonia/PayCalc24.Client.Avalonia.csproj -c Release -r osx-arm64 --self-contained true`
- Output: `src/PayCalc24.Client.Avalonia/bin/Release/net9.0/osx-arm64/publish/`
- Executable: `PayCalc24.Client.Avalonia` (Mach-O 64-bit arm64)
- `.app`: not produced; raw executable publish output is smoke-testable.
- Smoke: PASS. Main window, Ribbon, Backstage, navigation/workspace, en-US/vi-VN, System/Light/Dark, SVG IconKey resolution, non-production demo marker, explicit `UNAVAILABLE`, pinned revision and clean exit verified.

### Windows x64

- RID: `win-x64`
- Command: `dotnet publish src/PayCalc24.Client.Avalonia/PayCalc24.Client.Avalonia.csproj -c Release -r win-x64 --self-contained true`
- Output: `src/PayCalc24.Client.Avalonia/bin/Release/net9.0/win-x64/publish/`
- Executable: `PayCalc24.Client.Avalonia.exe` (PE32+ x86-64)
- Publish: PASS with no platform-specific warnings or errors.
- Smoke launch: NOT EXECUTED — no Windows runtime environment. Wine/emulation was not used.

## Known limitations

- Evaluation builds can show the official Actipro prompt; production binaries require the externally supplied commercial license.
- macOS output is an unsigned raw executable directory, not an `.app` bundle and is not notarized.
- Windows execution smoke remains to be run on native Windows.
- Production MariaDB and external providers were not connected during desktop smoke testing.
- CI: record the authoritative GitHub Actions URL after push.

Next project step: **POST-MVP TECHNICAL DOCUMENTATION + RELEASE CANDIDATE VALIDATION**. This is not Task 21 or Task 20A.
