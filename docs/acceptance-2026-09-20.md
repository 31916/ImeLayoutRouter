# Acceptance record — 2026-09-20

**Publication approved with incomplete compatibility coverage.** On 2026-09-20,
after browser automation was blocked and the incomplete tests were disclosed,
the owner explicitly authorized release. Unrun cases below remain unverified.
V1 1.1.0 / V2 2.0.0 will be distributed from the Japanese/English page at
https://31916.ch/IMELayOutRouter/. GitHub Releases is no longer used.
Only that subdirectory was added to the portfolio; no root navigation was changed.

## Completed evidence

Desktop environment: Windows build 26200, Google Japanese Input 3.34.6260.0,
Deutsch (Schweiz) keyboard layout. Chrome 153.0.8010.48 is installed; its browser
acceptance remains pending below. The input list remains de-CH plus Japanese/Google.

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
- The repaired V1 and V2 installers then passed the complete lifecycle script on the user's
  PC, including both own-startup removal and preservation of another edition's
  startup registration. The existing prototype settings were unchanged.
- Development installer bytes/hashes (not public release approval):
  - V1 `1.1.0-rc.1`: 51,343,668 bytes,
    `2d462ebfd34258903c4b5ae3b477662efba589f5002e3e86d66a48fdc5d1e5e4`.
  - V2 `2.0.0-rc.1`: 51,359,388 bytes,
    `b31ccded2bde9d7120b12bb2856d17f17a6887c4612081db5a45058328e7acf5`.
  These are historical RC artifacts, not the final distribution files.
- In a native WinForms fixture running the actual routing engine, both editions
  passed physical-key Google Japanese Input composition (`a` → `あ`, then Enter)
  and Swiss German layout punctuation (`Ctrl+Right Alt+2` → `@`). No Unicode paste
  or synthetic composition events were used. This is evidence for this native
  fixture, not a browser email field or every native application.
- Earlier desktop checks passed native/direct routing, explicit native restoration,
  password metadata, and V2 executable exclusion, pause/resume and manual restoration.
  Local logs live in the workspace `outputs` directory, outside the public site.
- V2 also passed actual Windows `WM_HOTKEY` delivery for Ctrl+Alt+F8, F9 and F10
  using physical key injection through Computer Use. The fixture unregisters the
  shortcuts when it loses focus or closes. This complements the separate live
  pause/target/restore routing tests; it is not a full tray-application walkthrough.
- V2 successfully loaded the user's legacy settings read-only and wrote a two-second
  diagnostic report containing profile names, handles, layout/mode and field flags.
  No entered text is collected. The original settings file was preserved.

## Fixes found during release preparation

- Disabling startup in one edition could remove the other edition's registration.
  It now removes only its own executable command.
- Uninstall left its startup registration pointing to a deleted executable. Each
  uninstaller now removes only a matching registration and preserves other editions.
- V1's installer was English-only. It now uses Japanese; V2 provides Japanese/English.
- `dotnet run` with custom edition output paths could execute a stale default-edition
  test binary locally. CI now builds and executes the exact edition DLL explicitly.

## Unverified compatibility / follow-up acceptance

- Browser email/URL/telephone/number/password fields, rapid focus changes, native
  return and target punctuation on Chrome/Edge and Firefox.
- Microsoft Japanese, Pinyin, Bopomofo and Korean IME desktop composition/routing.
  The user declined adding input methods to this PC; do not change the installed
  language/input list. These cases need another suitable Windows test environment.
- Complete startup-at-sign-in and diagnostic-save UI acceptance, a complete installed-app
  walkthrough, and the remaining historical-version upgrade cases.

## Final package publication checks

Before upload, require successful final-version CI, installer lifecycle checks,
per-file SHA-256/size and preserved icon. After upload, verify HTTPS downloads
against those hashes. Record final package evidence separately below.

Re-run `tools/Test-Installer.ps1` on final packages. For composition, build
`tests/WindowsSmoke/WindowsSmoke.csproj` for the required edition and execute its
edition-specific DLL with `--composition` in an interactive desktop session.
The fixture never changes installed input methods or saves entered text.

## Final distribution packages — 2026-09-20

Built from application commit `fdfe2a5c0e21f9d8d103fb2848274323c70c674f` in
[successful Windows CI run 35482636083](https://github.com/31916/ImeLayoutRouter/actions/runs/35482636083).
Both edition jobs passed; the full regression suite passed 30 scenarios.
The downloaded artifact ZIPs matched the SHA-256 digests returned by GitHub.

| Edition | Installer | Bytes | SHA-256 |
| --- | --- | ---: | --- |
| V1 | ImeLayoutRouter-V1-1.1.0-Setup.exe | 51,342,344 | `64f48a8db7ed0c5da1f68b0ee772add861463e14d31ed3af9851f548ce73b22e` |
| V2 | ImeLayoutRouter-V2-2.0.0-Setup.exe | 51,365,971 | `510616cb7a1be937ea4b546a99cfe8790295d67115519637539eb91c1e2ac088` |

Both exact final installers passed `tools/Test-Installer.ps1` on the user's PC:
install, same-package reinstall, shared-instance startup, own-startup cleanup,
other-edition startup preservation and unchanged existing settings. Additional
checks verified the executable and uninstall registration versions and that the
installed `Assets/app.ico` matches the canonical original icon byte-for-byte.
Local evidence: `outputs/lifecycle-final-simple.log` and `lifecycle-final-full.log`.
The icon SHA-256 is `f97e2a2030ff6e8b77fc2240a7690a206081d038d3a87a37165be4aee0f87857`.
The browser and CJK compatibility limitations above are unchanged.
