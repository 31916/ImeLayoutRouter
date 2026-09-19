# IME Layout Router

A Windows tray utility that routes IME direct/Latin input to your preferred
keyboard layout, while keeping native Japanese, Chinese and Korean input
available.

```text
あ / 中文 / 한글  →  native IME input
A / Latin input  →  your target layout, e.g. Deutsch (Schweiz)
```

## Branches / ブランチ

| Branch | Role | Default build |
| --- | --- | --- |
| [V1](https://github.com/31916/ImeLayoutRouter/tree/V1) | 日本語簡易版 / Japanese essentials | `Simple` |
| [V2](https://github.com/31916/ImeLayoutRouter/tree/V2) | メイン・多機能版 / Main development branch | `Full` |

V1 includes the repaired routing engine, Japanese-only setup, source/target
selection and startup. V2 adds CJK selection, Japanese/English UI, application
rules, status, shortcuts and guided setup. Fixes are shared; V1 excludes the
advanced UI and runtime modules at build time. `dotnet build -c Release` uses
the branch's default. Override with `-p:Edition=Simple` or `-p:Edition=Full`.

**Both branches are unreleased development code.** No new public release or
download may be published before its desktop acceptance tests pass. The intended
download page is `31916.ch/IMELayOutRouter/`, without a portfolio entry link. See the
[release plan](docs/release-plan.md) and [website preview instructions](site/README.md).
The internal v3 package is not being published under this new plan.
The previously published v1.0.0 does not include the email-field repair.
Chinese/Korean support and the new field detector are preview features; see
the [validation status and acceptance matrix](docs/diagnosis.md).

## Download and upgrade

[Download page](https://31916.ch/IMELayOutRouter/) — Japanese and English.
Installers will be distributed directly from this website after desktop acceptance.
The old prototype release has been retired; GitHub Releases is no longer used.

For an installer release, download `ImeLayoutRouter-Setup.exe`.
For a portable preview, extract the complete ZIP and run `ImeLayoutRouter.exe`.
The self-contained Windows x64 package does not require a separate .NET install.

1. Exit an older running copy using its tray menu before upgrading from v1.
2. Start the new application and open **Settings**.
3. Select a **Source IME** and **Target Keyboard Layout**.
4. Optionally enable **Route all enabled Japanese, Chinese and Korean IMEs**.
5. Save. Legacy `settings.json` can be imported. New saves use `settings-v1.json`
   or `settings-v2.json` and preserve the original file. Only one edition runs at a time.

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

## V2 tray controls

- **Settings** — select the input language(s) and target layout.
- **Pause routing** — suspend/resume automatic changes.
- The tray tooltip and menu show the observed layout/mode and routing reason.
  A sent request is shown as pending until its result is observed.
- **Save diagnostic log (30 seconds)** — record input state while reproducing
  a problem. No text values, keystrokes, document contents, titles or URLs are
  intentionally read or logged.
- **Exit** — stop the application.

A second launch opens the existing instance's settings, including across editions.
A monitor failure is reported through the tray rather than silently stopping.

## Priority features (development build, not yet released)

Settings now has four guided pages, displayed in Japanese or English according
to the Windows UI language: IME/layout, applications, shortcuts and a typing check.
Select the source and target explicitly on first use. **Try these settings** runs
only in the settings window, without saving or enabling global shortcuts. Test
text stays in those controls; it is not read, saved or sent by the router.
Application rules are not applied to this local exercise.

Application rules match the full executable path of the process owning the
focused control. Choose automatic routing or disable it, with a default for
unlisted applications. A disabled default plus automatic exceptions creates an
allowlist. If a process cannot be identified while rules exist, automatic routing
is suppressed. Hosted applications may need a rule for their actual input host.
Rules do not use window titles, URLs or typed content.

Global shortcuts are **off by default**, including after migrating old settings.
When enabled, the defaults are Ctrl+Alt+F8 (pause/resume), Ctrl+Alt+F9 (target
layout), and Ctrl+Alt+F10 (previous layout in the same window). Each key can be
changed to F6–F11. A registration conflict disables the whole set and is reported
in the tray; choose different keys in Settings. Manual switches work while
automatic routing is paused or excluded. They are held until leaving that window
or pausing and resuming. Restore requires a layout observed before a successful
request in the same window; it does not force the IME into a conversion mode.

Settings schema 3 preserves schema 1/2 selections and adds these preferences.
Back up settings before downgrading: older binaries cannot read schema 3.
See [priority feature validation](docs/priority-features.md) for the remaining
interactive checks. These changes do not constitute a V1/V2 release.

## Version progression

| Version | Changes |
| --- | --- |
| 1.0.1 repair | Persistent A detection, InputScope field fallback, conversion-state checks, timeout/retry/confirmation and regression tests |
| 2.0.0-preview.1 | Japanese, Chinese (including regional variants) and Korean source selection; language-specific native restoration |
| 3.0.0-preview.1 | Route all enabled CJK IMEs, explicit language-level routing, v1 settings migration and atomic saves, pause, diagnostics, single-instance control and build automation |

This is a progression of implementation milestones, not a
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
- Foreground/focus events and new field metadata wake the monitor; a 50 ms
  fallback detects changes that do not emit events. The accessibility worker
  retains its 100 ms fallback. Slow providers and asynchronous application
  handling still add latency. No keystrokes are intercepted or buffered, so
  an immediate first keystroke can still precede routing.

## Build and test

Requirements: Windows and the .NET 8 SDK.

```powershell
dotnet build -c Release
dotnet run --project tests/RoutingTests.csproj -c Release
dotnet run --project tests/UiSmoke/UiSmoke.csproj -c Release
dotnet publish -c Release -r win-x64 --self-contained true
```

An optional desktop test opens its own empty controls and checks native mode,
direct-input routing, password metadata, exclusion, pause and manual restoration.
Exit other router instances first. It requires foreground focus, cancels on
deactivation, changes only its own window's layout, and restores that layout
on exit. See the validation record for which live checks have actually passed.

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
