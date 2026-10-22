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
Windowsの未確定文字ウィンドウに限定します。モニター左上付近の小窓を識別し、
描画領域を一時的に空にします。入力位置が小窓の近くにある場合は対象外です。
候補ウィンドウ、通常のアプリウィンドウ、独自の描画方式のIMEは対象外です。
WindowsやIMEの全バージョンに対応することは確認できていません。

設定をオフにする、入力位置が正常な場所へ移る、アプリを切り替える、または
V3を終了すると、変更した描画領域を戻します。補助プロセスは親アプリの終了を検出して
復元します。補助プロセス自体が異常終了した場合のために、ウィンドウ識別情報と
元の描画領域だけを `%LOCALAPPDATA%\ImeLayoutRouter\v3-recovery` に保存します。
入力文字、キー、ウィンドウタイトルは取得・保存しません。

設定は `settings-v3.json`、自動起動は `ImeLayoutRouterV3` という専用の項目です。
V1・V2とは設定・起動登録を分けているため、既存版と同時に利用できる構成です。
従来のアプリアイコンを使用し、設定画面は日本語と英語を用意しています。

## Validation gates

- Policy/settings checks and synthetic native-window tests are automated.
- Native tests create a separate Win32 desktop without switching the user's display.
  They do not send keys, move the mouse, install an IME, or alter the default layout.
- Synthetic windows test classification, cross-process masking, exact restoration,
  candidate exclusion, owner-hide behavior and recovery after helper termination.
  **They do not demonstrate real IME composition compatibility.**
- On the development PC, the non-displayed desktop exposed no enabled Japanese IME.
  Subsequent unsigned test builds were blocked by Windows Code Integrity; the policy
  was not changed. A separate Windows CI workflow runs the development tests.
- Before release: validate actual composition, conversion candidates, confirmation,
  cancellation, application switching, multiple monitors/DPI, sleep/resume and exit
  with the supported IMEs. No release installer or download-page update is authorized
  by a passing synthetic test alone.

## Build / check

```powershell
dotnet build -c Release -p:Edition=Guard --warnaserror
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
- [Korean IME top-left composition report in WebView2](https://github.com/MicrosoftEdge/WebView2Feedback/issues/5675)
  confirms that the symptom is not limited to Japanese. Chinese IMEs also use
  composition, but Chinese behavior has not been validated in this project.
