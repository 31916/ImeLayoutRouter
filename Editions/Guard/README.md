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
描画領域を一時的に空にします。入力位置が小窓の近くにある場合は対象外です。
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
V3のオン・オフで変わらないことを確認しました。Microsoft IME、他のブラウザーやゲーム、複数モニター、復帰動作は
まだ検証完了していないため、完成版としては公開していません。

## Validation gates

- Policy/settings checks and synthetic native-window tests are automated.
- [Run 39868972814](https://github.com/31916/ImeLayoutRouter/actions/runs/39868972814)
  rechecked all 53 policy/native checks and installer lifecycle tests at commit
  `2635dbe`. Upgrade and uninstall while running passed, saved choices survived
  an upgrade, and both operations refused to continue with an invalid recovery
  record. Japanese/English settings renders from the disposable VM were visually
  reviewed; controls, text and the existing icon are visible without clipping.
- Native tests create a separate Win32 desktop without switching the user's display.
  They do not send keys, move the mouse, install an IME, or alter the default layout.
- Synthetic windows test classification, cross-process masking, exact restoration,
  candidate exclusion, owner-hide behavior and recovery after helper termination.
  **They do not demonstrate real IME composition compatibility.**
- On 2026-10-23 (JST), [CI run 39864840010](https://github.com/31916/ImeLayoutRouter/actions/runs/39864840010)
  passed 25 policy/settings checks, 28 native fixture checks and the development
  installer lifecycle checks. This includes helper DPI initialization, moving a
  popup away from/back to the corner, recovery after helper termination, and
  uninstalling while the app is running. The synthetic move-to-mask observations
  were 53–82 ms across six trials; these are not real IME first-frame measurements.
  Recovery also checks interrupted unpublished records and prevents a bounded
  batch from reporting success while additional restoration records remain.
- [Google IME run 39864840012](https://github.com/31916/ImeLayoutRouter/actions/runs/39864840012)
  passed actual Japanese input on Windows Server 2025 with Google Japanese Input
  3.34.6260.0. Real keystrokes produced `にほん`; both baseline and suppression-on
  conversion committed `日本`. Escape cancellation, preserving the pending text,
  restoring the real popup when disabled and leaving normal positioning drawable
  passed. The captured baseline/masked/candidate images were visually reviewed:
  only the fallback composition frame disappeared and conversion candidates
  remained visible. This verifies that specific fixture and IME/OS combination.
  Candidate visibility, switching to another fixture thread, a real Win32 caret
  near the corner and ten consecutive composition/cancellation cycles also passed.
  Show-event to empty-region observations were 15–94 ms in that run, and 31–125 ms
  in [run 39864035466](https://github.com/31916/ImeLayoutRouter/actions/runs/39864035466).
  These include event delivery, masking and observation delay; they are not
  first-rendered-frame/video measurements and do not prove zero-frame prevention.
- [Chrome compatibility run 39865503247](https://github.com/31916/ImeLayoutRouter/actions/runs/39865503247)
  passed real Google IME typing in Chrome's ordinary text input and textarea.
  With V3 off and on, both fields received `にほん`, emitted real composition
  events and committed the same `日本` result. Chrome used a new, isolated profile
  and a local HTML fixture. CDP prepared/read that page; OS keystrokes produced
  the text. This checks ordinary browser input compatibility, not every website
  or a reproduction of a specific browser's positioning bug.
- [Run 39868972851](https://github.com/31916/ImeLayoutRouter/actions/runs/39868972851)
  rechecked the full Google IME/native/Chrome suite at `2635dbe`. The fixture
  verifies that the requested TSF profile is active and uses a native message
  pump throughout composition. Its ten show-to-mask observations were 47–94 ms.
- Before release: validate actual composition, conversion candidates, confirmation,
  cancellation, application switching, multiple monitors/DPI, sleep/resume and exit
  with the supported IMEs. No release installer or download-page update is authorized
  by a passing synthetic test alone.
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
- [Korean IME top-left composition report in WebView2](https://github.com/MicrosoftEdge/WebView2Feedback/issues/5675)
  confirms that the symptom is not limited to Japanese. Chinese IMEs also use
  composition, but Chinese behavior has not been validated in this project.
