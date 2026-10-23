# IMELayoutRouter V3 — development version

V3 is a standalone utility for hiding the Windows fallback composition popup
when it appears at the top-left of a monitor. V1/V2 integration is deferred.
This branch is **not a released or fully validated product**.

## 日本語

入力途中の未確定文字が画面左上の小窓に出る現象を対象にしています。
IMEやキーボード配列の切り替え、入力内容の削除・確定は行いません。
元のアプリが入力位置を正しく通知できない問題そのものを修正する機能ではありません。
小窓にだけ表示される未確定文字は、この機能をオンにすると見えなくなります。

対象は、前面アプリと同じスレッドが持つ `MSCTFIME Composition` という
Windowsの未確定文字ウィンドウと、その部品を内包することを確認できた
`CiceroUIWndFrame` の外枠に限定します。外枠はIMEの所有関係も確認し、
別の種類の表示部品が含まれていれば対象外にします。モニター左上付近の小窓を識別し、
描画領域を一時的に空にします。Windowsから取得できたカーソル位置が小窓の近くにある場合は対象外です。
カーソル位置を取得できないアプリでは、左上が正しい表示位置でも隠れる場合があります。
候補ウィンドウ、通常のアプリウィンドウ、独自の描画方式のIMEは対象外です。
WindowsやIMEの全バージョンに対応することは確認できていません。
V3より高い権限で動くアプリの小窓は、Windowsのアクセス制限により変更できません。
現在の試験版は、その制限を回避したり自動で管理者権限を要求したりしません。

設定をオフにする、入力位置が正常な場所へ移る、アプリを切り替える、または
V3を終了すると、変更した描画領域を戻します。補助プロセスは親アプリの終了を検出して
復元します。補助プロセス自体が異常終了した場合のために、ウィンドウ識別情報と
元の描画領域だけを `%LOCALAPPDATA%\ImeLayoutRouter\v3-recovery` に保存します。
入力文字、キー、ウィンドウタイトルは取得・保存しません。

設定は `settings-v3.json`、自動起動は `ImeLayoutRouterV3` という専用の項目です。
V1・V2とは設定・起動登録を分けているため、既存版と同時に利用できる構成です。
従来のアプリアイコンを使用し、設定画面は日本語と英語を用意しています。
上書きインストール・アンインストールは表示の復元を先に行い、復元できない場合は
処理を中止します。上書き時は保存済みの設定と自動起動の選択を保持します。

2026年10月23日（日本時間）、使い捨てのWindows Server 2025環境で
Google日本語入力3.34.6260.0の実入力・変換・確定・取消し・表示復元を確認しました。
通常位置の表示、左上に実際のカーソルがある場合の表示、前面切り替え、10回連続の入力も
確認済みです。Chromeの通常の入力欄・複数行入力欄でも、日本語入力と変換結果が
V3のオン・オフで変わらないことを確認しました。同日、Windows 11 ARM（build 26200）の
Microsoft日本語IMEでも、同じ入力・候補保持・表示抑制・復元の検証に成功しました。
候補一覧は実際の描画とキー操作で照合し、画像も目視確認しています。
他のブラウザーやゲーム、Windows 11 x64、複数モニター、復帰動作は
まだ検証完了していないため、完成版としては公開していません。

## Validation gates

- [Native and installer run 39877751275](https://github.com/31916/ImeLayoutRouter/actions/runs/39877751275)
  passed at `7f21609`: 25 policy/settings checks, 38 native checks, and development
  installer lifecycle checks. All 200 cross-process move/restore/remask cycles
  and 100 custom-region move/restore cycles passed. The ordinary-region
  move-to-mask observations were 31–95 ms, not real IME first-frame latency.
  Japanese/English settings renders retain the existing icon and were visually
  reviewed for missing text, overlap and clipping.
- A rare restoration failure was reproduced in
  [run 39871343642](https://github.com/31916/ImeLayoutRouter/actions/runs/39871343642).
  During a window move, `GetWindowRgn` briefly returned 0 and then 1 for the same
  masked window. The old code mistook the ambiguous 0 for an owner replacement,
  removed its recovery token and left an empty region. The fix keeps ownership
  on an ambiguous query and explicitly restores an originally unbounded window.
  A saved custom region is rechecked after a bounded settling interval. Regression
  tests inject the unavailable query and also verify deliberate owner changes.
  Diagnostic traces and query injection are compiled only into the synthetic
  test executable, never the application or real-IME fixture.
- Native tests use synthetic windows on a separate, non-displayed Win32 desktop.
  They check classification, candidate exclusion, owner-hide/change behavior,
  custom-region restoration, helper EOF, abrupt helper termination, interrupted
  recovery records and bounded recovery batches. **These tests do not establish
  real IME compatibility.**
- Installer tests cover installation, upgrade/uninstall while running, preserving
  saved choices on upgrade, refusing both operations when recovery is incomplete,
  and retaining V1/V2 startup registration. No release installer is uploaded.
- [Google IME and Chrome run 39877751312](https://github.com/31916/ImeLayoutRouter/actions/runs/39877751312)
  passed at `7f21609` on Windows Server 2025 with Google Japanese Input 3.34.6260.0.
  Actual keystrokes produced `にほん`; conversion committed `日本` with V3 off/on.
  The baseline/masked/candidate images show that only the fallback composition
  frame disappeared and candidates remained visible. Cancellation, preserving
  pending text, disabling suppression, normal positioning, a real corner caret,
  switching input threads and ten repeated input cycles passed. The ten
  show-event-to-empty-region observations were 32–125 ms. These measurements include
  event delivery and observation delay; they do not demonstrate zero-frame hiding.
- The same Google IME run passed ordinary Chrome text input and textarea checks:
  both emitted actual composition events and committed the same `日本` result
  with V3 off/on. Chrome uses a unique profile, a local test page, and its verified
  document window. CDP prepares/reads that page; OS keystrokes produce the text.
  This is input compatibility evidence, not coverage of every website.
- [Microsoft IME run 39871172870](https://github.com/31916/ImeLayoutRouter/actions/runs/39871172870)
  passed its independent Chrome text/textarea input stage at `d29a698` on Windows
  Server 2025. Actual preedit was `にほん` and the committed result was `日本`
  with suppression off/on. The separate raw-window fallback stage failed before
  V3 was enabled: the IME immediately committed each kana in that fixture.
  The overall run is failed and does not establish Microsoft fallback-popup
  suppression compatibility.
- [Windows 11 Microsoft IME run 39874656891](https://github.com/31916/ImeLayoutRouter/actions/runs/39874656891)
  passed on ARM64 build 26200 after validating repair revision `7f21609` on the
  same prepared disposable VM. The workflow's initial `e49d608` revision failed
  candidate observation; it is not the passing revision. Both browser and native
  fixture exit codes were 0 at `7f21609`. Real preedit `にほん`, conversion `日本`,
  candidate selection, cancellation, focus changes, disable/restore, normal
  positioning, a real corner caret and ten repeated input cycles passed.
  Candidate APIs exposed no IMM/TSF list or UIA descendants on this image, so the
  fixture compared the rendered panel and actual Up/Enter results. All 1,965
  measured candidate glyph pixels matched with V3 off/on. Full screenshots were
  also reviewed. Show-event-to-empty-region observations were 62–125 ms, not
  first-frame latency. ARM64 evidence does not establish Windows 11 x64 coverage.
  The original repair artifact copied an older `v3-ime-failure.png` forward;
  `revision.txt`, the checks log and baseline/enabled images identify the actual
  result. Later repair collection excludes files predating the tested revision.
- One earlier Google browser baseline at `319fef3`
  ([run 39877451559](https://github.com/31916/ImeLayoutRouter/actions/runs/39877451559))
  failed before V3 was enabled: Chrome received only `honn` with no composition
  events. Later independent runs passed, but the cold-start fixture/input-service
  readiness issue is not considered resolved by those passes.
- Other browsers/games, Windows 11 x64, multiple physical monitors/DPI and
  sleep/resume remain separate release gates. A passing synthetic test alone
  does not authorize a release installer or download-page update.
- The helper reacts to foreground/window show, hide, destroy and position events,
  with a 100 ms fallback scan and 10 ms event-burst coalescing. Notifications run
  outside the target process and do not inspect text. The helper uses per-monitor
  V2 DPI awareness. A brief initial appearance is still possible; zero-frame
  suppression has not been demonstrated and is not a feature claim.

### Real IME integration fixture

The `V3 real IME compatibility` workflow provisions Japanese basic typing on
disposable GitHub-hosted Windows VMs only. It preserves the VM's display
language. Provisioning is not a passing input test. The separate fixture sends a
fixed test word to its own Win32 window, uses that window's input context to
simulate misplaced composition coordinates, and compares IME conversion results
with suppression off/on. It also checks cancellation, disabling suppression and
normal composition positioning. It stops sending keys if its window loses focus.

Both the provisioning script and fixture refuse to execute outside GitHub-hosted
runners. They must never be run on the user's PC. The fixture can read its own
test composition strings; that code is excluded from the production application.
The workflow retains test evidence, not a release installer. Passing this fixture
alone does not establish compatibility with browsers, games, other IMEs, multiple
monitors or all Windows versions.

The separate `V3 Google IME compatibility` workflow installs a pinned Google
Japanese Input MSI from Google's download service. It checks both SHA-256 and
the Google Authenticode signature before installation. All installation and
input testing stays on the disposable VM. Google Japanese Input reproduced the
corner popup as a `CiceroUIWndFrame` containing a visible `MSCTFIME Composition`
child; checking top-level composition windows alone missed this real case.
The classifier now verifies that frame's IME owner and composition children.
Candidate windows and unrelated Cicero frames are excluded.

Microsoft IME capability provisioning runs only when manually dispatched.
Windows Update downloads and cumulative-update servicing can take many minutes
on hosted images. A provisioning timeout is an environment failure, not a passed
IME compatibility test; its CBS/DISM evidence is retained separately.
Before downloading input packages, the workflow checks whether its own fixture
can receive focus. Windows 11 hosted images can show a first-logon privacy page;
the preparation script applies Microsoft's managed-device policy and identifies
that page's host by package identity. Any focus click must land on the fixture's
own uncovered client area. These steps are restricted to the disposable VM.
Ordinary browser typing and fallback-popup reproduction run separately; passing
one never substitutes for the other.
Modern Microsoft IME candidates can be absent from the legacy IMM candidate API.
The fixture can also read `ITfCandidateListUIElement` on its own TSF thread,
checking visibility, candidate text and selection without changing the IME UI.
If neither candidate API supplies a list, the fixture checks the Microsoft input
host's accessibility tree from an MTA thread while pumping the owning STA. If
that tree is also empty, the fixed 1024×768 fixture compares a candidate-only
screen rectangle outside the preedit popup and test window. It requires actual
glyph changes after conversion, at least 500 dark glyph pixels and preservation
of at least 95% of baseline glyph pixels/count, plus the expected second-to-first
candidate selection and committed result. This is a fixture-specific visual
regression check, not OCR or a universal candidate detector. The full before/
after screenshots remain subject to human review. All this observation code is
excluded from the application.

An optional, explicitly dispatched 10-minute repair window keeps the prepared
VM available for new V3 revisions after a failure. Each exact revision and result
is recorded separately. The workflow passes only if both input tests pass for
the initial revision or one recorded repair revision; failed evidence is retained.

### Reviewed screenshots (2026-10-23 JST)

These contain only fixed fixture input on disposable VMs. They are evidence for
the tested environment, not a promise for every application.

| IME | V3 off | V3 on |
| --- | --- | --- |
| Microsoft Japanese IME / Windows 11 ARM | [Composition and candidates](../../docs/v3-validation/microsoft-before.png) | [Candidates preserved, composition hidden](../../docs/v3-validation/microsoft-after.png) |
| Google Japanese Input / Windows Server 2025 | [Composition and candidates](../../docs/v3-validation/google-before.png) | [Candidates preserved, composition hidden](../../docs/v3-validation/google-after.png) |

## Build / check

```powershell
dotnet build -c Release --warnaserror # V3 branch defaults to Edition=Guard
dotnet build tests/GuardTests/GuardTests.csproj -c Release -p:Edition=Guard --warnaserror
dotnet tests/GuardTests/bin/Guard/Release/net8.0-windows/GuardTests.dll
./tools/Test-GuardDesktop.ps1 -Assembly "$PWD/tests/GuardTests/bin/Guard/Release/net8.0-windows/GuardTests.dll" -OutputPath "$PWD/v3-native-checks.txt"
```

## Sources / applicability

- [Microsoft: composition and candidate windows](https://learn.microsoft.com/en-us/windows/win32/intl/status--composition--and-candidates-windows)
- [Microsoft: WM_IME_SETCONTEXT](https://learn.microsoft.com/en-us/windows/win32/intl/wm-ime-setcontext)
  describes in-application UI control; it is not a universal external suppression switch.
- [Microsoft: SetWindowRgn](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowrgn)
  defines region ownership and drawing behavior used here.
- [Microsoft: SetProp and UIPI](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setpropw)
  limits window-property changes to processes at the same or a lower integrity level.
- [Microsoft: LoadKeyboardLayout](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-loadkeyboardlayoutw)
  requires the caller to own keyboard focus on Windows 8 and later; the browser
  fixture prepares its input language before handing focus to Chrome.
- [Microsoft: TSF candidate UI](https://learn.microsoft.com/en-us/windows/win32/api/msctf/nn-msctf-itfcandidatelistuielement)
  supplies candidate count, selection and strings for the integration fixture.
- [Korean IME top-left composition report in WebView2](https://github.com/MicrosoftEdge/WebView2Feedback/issues/5675)
  confirms that the symptom is not limited to Japanese. Chinese IMEs also use
  composition, but Chinese behavior has not been validated in this project.
