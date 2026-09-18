# IME Layout Router

A Windows tray utility that routes IME direct/Latin input to your preferred
keyboard layout, while keeping native Japanese, Chinese and Korean input
available.

```text
あ / 中文 / 한글  →  native IME input
A / Latin input  →  your target layout, e.g. Deutsch (Schweiz)
```

**Current development version: 3.0.0-preview.1.**
The previously published v1.0.0 does not include the email-field repair.
Chinese/Korean support and the new field detector are preview features; see
the [validation status and acceptance matrix](docs/diagnosis.md).

## Download and upgrade

[Published releases](https://github.com/31916/ImeLayoutRouter/releases)

For an installer release, download `ImeLayoutRouter-Setup.exe`.
For a portable preview, extract the complete ZIP and run `ImeLayoutRouter.exe`.
The self-contained Windows x64 package does not require a separate .NET install.

1. Exit an older running copy using its tray menu before upgrading from v1.
2. Start the new application and open **Settings**.
3. Select a **Source IME** and **Target Keyboard Layout**.
4. Optionally enable **Route all enabled Japanese, Chinese and Korean IMEs**.
5. Save. Existing v1 settings are read automatically; saving upgrades the
   settings format, so keep a backup if you plan to downgrade.

Enable **Start IME Layout Router with Windows** to start at sign-in.
A portable application's folder must stay in place when startup is enabled.

## Why email fields were missed

Some IMEs temporarily force direct input for email, URL or password fields
without changing the open/native state visible through Windows input APIs.
Google's [Mozc InputScope design](https://github.com/google/mozc/blob/master/docs/design_doc/input_scope.md)
describes exactly this behavior. Version 1 watched only open/closed transitions.

The repaired monitor combines:

- IME open state and conversion mode.
- Focused field metadata for email, URL, telephone, number and password fields.
- Persistent-state checks, throttled retries and observed layout confirmation.
- Focus-aware native restoration, with a deadline if the field refuses it.

In a normal field, returning from the target layout to an IME requests native
input. Japanese restores hiragana; Chinese/Korean retain their width preferences.
Full-width Latin is preserved in ordinary fields.

## Tray controls

- **Settings** — select the input language(s) and target layout.
- **Pause routing** — suspend/resume automatic changes.
- **Save diagnostic log (30 seconds)** — record input state while reproducing
  a problem. No text values, keystrokes, document contents, titles or URLs are
  intentionally read or logged.
- **Exit** — stop the application.

A second v3 launch opens the existing instance's settings.
A monitor failure is reported through the tray rather than silently stopping.

## Version progression

| Version | Changes |
| --- | --- |
| 1.0.1 repair | Persistent A detection, InputScope field fallback, conversion-state checks, timeout/retry/confirmation and regression tests |
| 2.0.0-preview.1 | Japanese, Chinese (including regional variants) and Korean source selection; language-specific native restoration |
| 3.0.0-preview.1 | Route all enabled CJK IMEs, explicit language-level routing, v1 settings migration and atomic saves, pause, diagnostics, single-instance control and build automation |

This is a progression of implementations in the development branch, not a
claim that every version has been published as a stable release.

## Compatibility and limits

- Windows x64; build target .NET 8.
- Only IMEs already **enabled in Windows** appear as sources.
- Standard non-CJK keyboard layouts are offered as targets.
- Routing is **language-level**. Selecting an IME also covers other enabled
  IMEs with the same Windows language ID. Cross-process HKL/IMM observation
  cannot reliably distinguish individual TSF profiles with a shared language.
  The all-IME option covers all enabled Japanese/Chinese/Korean source languages.
- The semantic field fallback uses MSAA/IAccessible2 `text-input-type` metadata
  and protected-field state. Applications that expose neither this information
  nor their effective IMM mode cannot be classified reliably. Unknown state
  alone never triggers a switch.
- InputScope-only custom controls, elevated applications, remote desktops,
  secure desktops and some TSF-only IMEs can require additional support.
- Chinese/Korean native composition and browser-specific behavior need the
  manual acceptance matrix before a stable release. Synthetic tests are not
  a substitute for those checks.
- The monitor polls every 100 ms; it does not intercept or buffer keystrokes,
  so an immediate first keystroke during a focus change can precede routing.

## Build and test

Requirements: Windows and the .NET 8 SDK.

```powershell
dotnet build -c Release
dotnet run --project tests/RoutingTests.csproj -c Release
dotnet publish -c Release -r win-x64 --self-contained true
```

An optional desktop test opens its own empty controls, verifies native mode,
routes a direct-input change, and checks password-field detection. Exit other
router instances first; it changes its own UI thread's input layout and restores
that layout on exit.

```powershell
dotnet run --project tests/WindowsSmoke/WindowsSmoke.csproj -c Release
```

Build the installer with [Inno Setup 6](https://jrsoftware.org/isinfo.php):

```powershell
iscc installer/ImeLayoutRouter.iss
```

The CI workflow builds and runs deterministic tests on Windows. The release
workflow builds preview ZIP and installer artifacts for manual review; it does
not automatically publish a stable release.

Read-only diagnostic CLI (works alongside an existing router):

```powershell
.\ImeLayoutRouter.exe --diagnose 30 .\diagnostic.log
```

Configuration is stored in `%LOCALAPPDATA%\ImeLayoutRouter\settings.json`.
See [the investigation](docs/diagnosis.md) and [change log](CHANGELOG.md).
