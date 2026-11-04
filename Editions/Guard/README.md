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

### 使い方

1. 起動後の設定画面で「左上の未確定文字の小窓を隠す」を選び、保存します。
2. 必要なら「Windowsと同時に起動する」を選びます。インストールだけでは自動起動は有効になりません。
3. 設定を変えるときは通知領域のアイコンをダブルクリックします。表示を戻して終了するときは、
   アイコンのメニューから「終了して表示を戻す」を選びます。

誤って必要な小窓まで隠れた場合は設定をオフにしてください。
上書き・アンインストール時に復元できないという表示が出た場合は、入力先のアプリを終了してから再実行してください。

### 確認した範囲

| 項目 | 確認できたこと |
| --- | --- |
| 入力・変換 | Google日本語入力とMicrosoft日本語IMEで、未確定文字を隠しても候補の選択・確定・取消しができました。Google日本語入力では200回の連続入力・取消しも確認しました。 |
| Chrome・Edgeの初回入力 | Google日本語入力で各ブラウザーを12回新規起動し、それぞれ48回の未確定文字と変換確定を確認しました。Windows 11 x64のMicrosoft日本語IMEでも、通常欄・複数行欄でオン・オフの結果が一致しました。 |
| 画面の位置・倍率 | Windows 11 x64の100%・125%の2画面で、左上の小窓を隠し、通常位置へ移すと描画が戻ることを確認しました。1画面の100%・150%でも検証済みです。 |
| 全画面表示 | 枠なし全画面で候補一覧が残ることを確認しました。OpenTTD 15.3ではウィンドウ表示・Win32全画面の双方で「日本」を確定できました。 |
| インストーラー | x64版の導入、初回設定中の終了、起動中の上書き・アンインストール、設定の保持を確認しました。Windows 11 ARMではx64エミュレーションで導入・起動しています。 |
| スリープ復帰 | 検証中です。正式版の配布はこの確認が済むまで保留しています。 |

初回入力の試験では、ページが描画され、Windows側のフォーカスとIMEが準備できてから最初の文字を1回だけ送ります。失敗した文字の再送は行いません。本体では、IMEの小窓自身が持つカーソルを入力先アプリのカーソルと誤認する問題を修正しました。

中国語・韓国語IME、すべてのゲームや物理GPUでの互換性は確認していません。[検証記録・比較画像](https://github.com/31916/ImeLayoutRouter/blob/V3/docs/v3-validation/README.md)で、確認した環境と結果を参照できます。

## English

V3 hides a misplaced Windows fallback composition popup at a monitor's top-left
corner. The text being composed in that popup becomes invisible; conversion
candidates remain available. V3 does not switch IMEs or keyboard layouts, change
the typed text, or repair the input application's focus and positioning bugs.

1. Open V3, select **Hide the corner composition popup**, and save.
2. Optionally select **Start with Windows**. Installation alone does not enable it.
3. Double-click the notification-area icon to change settings. Use **Exit and
   restore display** from its menu to stop V3 and restore the affected windows.

Turn the option off if a popup you need is hidden. A legitimate corner popup can
also be hidden when Windows exposes no application caret position. Custom-rendered
IME interfaces and applications running at a higher privilege level are outside
the supported target. A brief initial appearance of the popup is still possible.
Compatibility with every IME, application and Windows version is not established.

V3 restores modified drawing regions when disabled, when the popup moves to a
normal position, on a foreground-application change, and on exit. Its separate
helper also handles parent-process termination. Recovery records contain only
window identity and original drawing-region data; typed text, keystrokes and
window titles are never read or recorded. The application makes no network requests.

Settings and startup registration are separate from V1/V2. Upgrades preserve
saved choices. Upgrade and uninstall stop if display recovery is incomplete;
close the affected input application and retry in that case. The installer and
settings screen support Japanese and English and use the original application icon.

## Validation

Detailed results, exact revisions, failed investigations and reviewed screenshots
are in the [validation record](https://github.com/31916/ImeLayoutRouter/blob/V3/docs/v3-validation/README.md).

| Check | Current evidence |
| --- | --- |
| Google Japanese Input / Chrome and Edge first input | 12 fresh profiles and 48 preedit/commit checks per browser passed |
| Microsoft Japanese IME / Windows 11 ARM | Real input, candidates, restoration and borderless fullscreen passed |
| Packaged x64 installer on x64 and ARM Windows | First-run shutdown, running upgrade/uninstall and saved choices passed |
| Actual display scale on one display | 100% and 150% passed |
| Microsoft Japanese IME / Windows 11 x64 | Chrome and Edge first input passed; real popup suppression, correction and commit passed on 100%/125% virtual monitors |
| Actual Windows S3 resume | Not yet verified; the stable installer remains gated |

Tests use disposable GitHub-hosted environments. They do not establish support
for every physical GPU, game, IME or language. Chinese and Korean IMEs have not
been validated; they are not included in the verified compatibility claims.

## Build

```powershell
dotnet build -c Release --warnaserror
dotnet publish -c Release -p:Edition=Guard -r win-x64 --self-contained true --warnaserror
```

Compile `installer/V3.iss` with Inno Setup 6 after publishing. Runtime input,
installer and display tests belong on disposable validation VMs; do not run the
IME fixture or its environment preparation scripts on a daily-use PC.
