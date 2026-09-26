# IMELayoutRouter (IME Layout Router)

[Official downloads / 公式ダウンロード](https://31916.ch/IMELayoutRouter/)
— IMEの直接入力を、指定したWindows入力レイアウトで。日本語簡易版V1と多機能版V2を配布しています。

A Windows tray utility that selects an existing Windows input layout for IME
direct/Latin input. Choose a source IME and a target layout already enabled in
Windows. V1 covers Japanese; V2 also offers Chinese and Korean source selection.

不具合の報告では、入力した文章・個人の文書・パスワードを添付しないでください。
診断ファイルを共有する前に、[公開時の注意と脆弱性の非公開報告先](SECURITY.md)を確認してください。
For safe diagnostic sharing and private vulnerability reports, see [SECURITY.md](SECURITY.md).

```text
あ / 中文 / 한글  →  native IME input
A / Latin input  →  your target layout, e.g. Deutsch (Schweiz)
```

## 動作の範囲 / What the app changes

**使用中のWindows入力レイアウトを切り替える機能はあります。** 対象IMEが直接入力
（A）になった場合や、メール欄などで直接入力が必要だと判定した場合に、入力中の
アプリへ、設定済みの切替先レイアウトを使うよう要求します。たとえば日本語IMEの
直接入力から、Windowsで有効にしたスイス・ドイツ語レイアウトへの切替です。
切替が受理されると、同じ物理キーから入力される文字や記号が変わることがあります。

**キーごとの割り当てや、配列そのものの定義を作成・編集する機能はありません。**
「AキーをBキーにする」などの独自リマップ、キーボードドライバーの書換え、
IMEやレイアウトの追加インストールは行いません。本書の「配列の切替」は既存の
入力レイアウトの選択を指します。すべてのアプリが切替要求を受け入れるわけではなく、
動作確認の範囲は後述の互換性情報と版別記事を参照してください。

**The active Windows input layout does change when the application accepts the
request.** When the selected IME uses direct input, or a supported field is
identified as requiring it, the router requests the configured target layout
for the application receiving input. For example, it can switch from a Japanese
IME to an enabled Swiss German layout. The same physical keys can then produce
different characters or punctuation.

**The app does not create or edit key mappings or layout definitions.** It does
not implement custom remaps such as A → B, rewrite keyboard drivers, or install
additional IMEs/layouts. "Layout switching" means selecting an existing Windows
input layout. An application can reject the request; see the compatibility
limits and edition notes for the scope of testing.

The implementation makes the decision in [RoutingPolicy.cs](RoutingPolicy.cs)
and sends the request in [RoutingMonitor.cs](RoutingMonitor.cs), using Windows'
[WM_INPUTLANGCHANGEREQUEST message](https://learn.microsoft.com/en-us/windows/win32/winmsg/wm-inputlangchangerequest).

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

**Ver1.1.1 (Japanese essentials) and Ver2.0.1 (full edition)** are the website distribution versions.
They include the browser field metadata initialization correction.
The owner approved publication on 2026-09-26 after the incomplete acceptance
coverage was disclosed. This does not turn unrun tests into passes.
Google Japanese Input with a Swiss German keyboard passed native Windows fixture
checks; browser email-field routing and Microsoft Japanese/Chinese/Korean IMEs
remain unverified. See the [current validation record](docs/browser-field-fix-2026-09-26.md)
and [original acceptance record](docs/acceptance-2026-09-20.md).
The old v1.0.0 prototype lacks the repaired engine. Internal 2.0/3.0 preview
milestones are not separate public editions.

## Download and upgrade

[Download page](https://31916.ch/IMELayoutRouter/) — Japanese and English.

Release notes: [Ver1.1.1](docs/releases/v1-1.1.1.md) / [English](docs/releases/v1-1.1.1.en.md),
[Ver2.0.1](docs/releases/v2-2.0.1.md) / [English](docs/releases/v2-2.0.1.en.md).
[Development history and date sources](docs/releases/history.md).

Installers are distributed directly from this website, with tested environments and limitations.
The old prototype release has been retired; GitHub Releases is no longer used.

Choose `ImeLayoutRouter-1.1.1-Setup.exe` (Japanese essentials) or
`ImeLayoutRouter-2.0.1-Setup.exe` (full edition). Check its SHA-256 against
the checksum file on the download page.
The self-contained Windows x64 package does not require a separate .NET install.

1. Exit an older running copy using its tray menu before upgrading from v1.
2. Start the new application and open **Settings**.
3. Select a **Source IME** and **Target Keyboard Layout**.
4. In V2, optionally enable **Route all enabled Japanese, Chinese and Korean IMEs**.
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

## V2 guided setup and application rules

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
interactive checks. The full acceptance coverage is still incomplete.

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
- Existing standard non-CJK keyboard layouts enabled in Windows are offered as
  targets. The app selects them; it does not create or modify their key mappings.
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
- Browser email/URL/telephone/number/password routing and Microsoft Japanese,
  Chinese and Korean IME composition remain unverified. The release includes
  their implementation without claiming verified compatibility. Synthetic tests
  are not a substitute for these checks.
- Foreground/focus events and new field metadata wake the monitor; a 50 ms
  fallback detects changes that do not emit events. The accessibility worker
  retains its 100 ms fallback. Slow providers and asynchronous application
  handling still add latency. No keystrokes are intercepted or buffered, so
  an immediate first keystroke can still precede routing.

## Build and test

Requirements: Windows and the .NET 8 SDK.

```powershell
dotnet build -c Release
dotnet build tests/RoutingTests.csproj -c Release -p:Edition=Full
dotnet tests/bin/Full/Release/net8.0-windows/RoutingTests.dll
dotnet build tests/UiSmoke/UiSmoke.csproj -c Release -p:Edition=Full
dotnet tests/UiSmoke/bin/Full/Release/net8.0-windows/UiSmoke.dll
dotnet publish -c Release -r win-x64 --self-contained true
```

An optional desktop test opens its own empty controls and checks native mode,
direct-input routing, password metadata, exclusion, pause and manual restoration.
Exit other router instances first. It requires foreground focus, cancels on
deactivation, changes only its own window's layout, and restores that layout
on exit. See the validation record for which live checks have actually passed.

```powershell
dotnet build tests/WindowsSmoke/WindowsSmoke.csproj -c Release -p:Edition=Full
dotnet tests/WindowsSmoke/bin/Full/Release/net8.0-windows/WindowsSmoke.dll
```

Build the installer with [Inno Setup 6](https://jrsoftware.org/isinfo.php):

```powershell
iscc installer/ImeLayoutRouter.iss
```

The CI workflows run Windows tests and package separate installer/portable
artifacts. Publication to the website is a separate reviewed step.

Read-only diagnostic CLI (works alongside an existing router):

```powershell
.\ImeLayoutRouter.exe --diagnose 30 .\diagnostic.log
```

Configuration is stored in `%LOCALAPPDATA%\ImeLayoutRouter\settings-v1.json`
or `settings-v2.json`; legacy `settings.json` is imported without replacing it.
See [the investigation](docs/diagnosis.md) and [change log](CHANGELOG.md).
