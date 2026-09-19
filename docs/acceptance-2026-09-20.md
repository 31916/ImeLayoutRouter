# Acceptance record — 2026-09-20

**Neither edition is approved for public installer distribution.** GitHub Releases
is no longer used. The Japanese/English information page is published at
https://31916.ch/IMELayOutRouter/ with both editions explicitly unavailable.
Only that subdirectory was added to the portfolio; no root navigation was changed.

## Completed evidence

- V1 and V2 build locally with warnings treated as errors.
- Full routing regression suite: 30 scenarios pass.
- Both editions pass settings schema 1/2/3 migration and startup ownership tests.
  V1 additionally passes Japanese-only source filtering, simple UI and stripping
  advanced preferences. V2 passes Japanese/English UI round trips and hotkey
  conflict rollback with a fake registrar.
- Windows CI run [35471497484](https://github.com/31916/ImeLayoutRouter/actions/runs/35471497484),
  application commit `f9b5408`, passes both edition builds, self-contained packaging,
  installer metadata, installation, same-package reinstallation, shared-instance
  startup, uninstall cleanup and preservation of another edition's startup value.
  Reinstallation is not evidence of every historical-version upgrade path.
- On the user's desktop, the pre-fix V1 installer reproduced stale startup
  registration after uninstall. The lifecycle test restores the pre-test startup
  value and preserves the installed old prototype and its settings.
- In a native WinForms fixture running the actual routing engine, both editions
  passed physical-key Google Japanese Input composition (`a` → `あ`, then Enter)
  and Swiss German layout punctuation (`Ctrl+Right Alt+2` → `@`). No Unicode paste
  or synthetic composition events were used. This is evidence for this native
  fixture, not a browser email field or every native application.
- Earlier desktop checks passed native/direct routing, explicit native restoration,
  password metadata, and V2 executable exclusion, pause/resume and manual restoration.
  Local logs live in the workspace `outputs` directory, outside the public site.

## Fixes found during release preparation

- Disabling startup in one edition could remove the other edition's registration.
  It now removes only its own executable command.
- Uninstall left its startup registration pointing to a deleted executable. Each
  uninstaller now removes only a matching registration and preserves other editions.
- V1's installer was English-only. It now uses Japanese; V2 provides Japanese/English.
- `dotnet run` with custom edition output paths could execute a stale default-edition
  test binary locally. CI now builds and executes the exact edition DLL explicitly.

## Still required before release

- Browser email/URL/telephone/number/password fields, rapid focus changes, native
  return and target punctuation on Chrome/Edge and Firefox.
- Microsoft Japanese, Pinyin, Bopomofo and Korean IME desktop composition/routing.
  The user declined adding input methods to this PC; do not change the installed
  language/input list. These cases need another suitable Windows test environment.
- Physical shortcut delivery, complete startup-at-sign-in and diagnostics acceptance,
  and the remaining historical-version upgrade cases.
- Final accepted installer versions, per-file SHA-256/size, direct website upload
  and download integrity verification. Do not upload unvalidated packages.

Re-run `tools/Test-Installer.ps1` on final packages. For composition, build
`tests/WindowsSmoke/WindowsSmoke.csproj` for the required edition and execute its
edition-specific DLL with `--composition` in an interactive desktop session.
The fixture never changes installed input methods or saves entered text.
