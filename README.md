# IMELayoutRouter V3 — Composition Guard

Windowsで、入力途中の文字が画面左上の小窓に出る現象を対象にした単体ツールです。
対象の小窓を隠し、オフにしたときや終了時には元の表示へ戻します。
**IMEやキーボード配列の切り替え、入力内容の削除・確定は行いません。**

現在は完成前の検証中です。V3の公開インストーラーはまだ配布していません。
[仕様・検証記録](Editions/Guard/README.md)で、実際に通ったテストと残っている確認を公開しています。

## 使い方と範囲

- 設定で「左上の未確定文字の小窓を隠す」を選びます。Windowsと同時に起動する設定もあります。
- 通常は通知領域に常駐します。設定をオフにするか「終了して表示を戻す」を選ぶと復元します。
- 小窓にだけ出ている入力途中の文字は見えなくなります。元のアプリの入力位置・フォーカスの不具合そのものは直しません。
- Windows標準の対象ウィンドウを識別します。独自描画のIMEや、より高い権限のアプリには適用できない場合があります。
- 変換候補は対象外です。アプリの正しいカーソル位置を取得できる場合は、その付近の小窓も対象外にします。
- 表示直後に短く見える可能性があります。「一瞬も表示されない」という機能ではありません。

日本語・英語の設定画面に対応し、既存のアプリアイコンを使用します。
入力文字・キー・ウィンドウタイトルは取得・保存せず、外部へ送信する機能もありません。
復元のため、ウィンドウ識別情報と描画領域だけをローカルに保存します。

## V1・V2との違い

| ブランチ | 役割 |
| --- | --- |
| [V1](https://github.com/31916/ImeLayoutRouter/tree/V1) | 日本語IMEの直接入力時に、設定済みのWindows入力レイアウトを選ぶ簡易版 |
| [V2](https://github.com/31916/ImeLayoutRouter/tree/V2) | 入力レイアウト選択にアプリ別ルールなどを加えた多機能版 |
| V3 | 左上の未確定文字の小窓を隠す単体版。入力レイアウトは変更しません |

V1・V2への機能追加は保留中です。V3は設定・自動起動登録が独立しています。
既存版のインストーラーは[公式ダウンロードページ](https://31916.ch/IMELayoutRouter/)を参照してください。

## Build

Windowsと.NET 8 SDKが必要です。このブランチの既定ビルドはV3です。

```powershell
dotnet build -c Release --warnaserror
dotnet publish -c Release -p:Edition=Guard -r win-x64 --self-contained true --warnaserror
```

ビルド後、Inno Setup 6で `installer/V3.iss` をコンパイルできます。
実入力・インストーラー・表示倍率・スリープの検証は、専用のGitHub-hosted使い捨てVMで行います。
`tests/GuardImeTests` や検証用の環境準備スクリプトを普段使うPCで実行しないでください。

## English

V3 is a standalone Windows tray utility that hides the fallback composition
popup when it appears at the top-left of a monitor. Turning suppression off or
exiting restores the display. It does not switch keyboard layouts or IMEs,
read typed text, press keys, or commit/delete input.

Uncommitted text visible only in that popup becomes invisible. V3 does not fix
the application's focus or positioning defect. Candidate windows are excluded;
custom IME rendering and higher-privilege applications may be unsupported.
A brief initial appearance remains possible. Settings are available in Japanese
and English, using the existing application icon.

V3 is still undergoing final validation; no public V3 installer is available.
See [scope and recorded validation](Editions/Guard/README.md). The V1 and V2
branches contain the existing layout-routing editions and remain separate.

## 不具合・脆弱性の報告 / Reporting

入力した文章、個人文書、パスワードを報告に添付しないでください。
利用したOS・IME・アプリと、個人情報を含まない再現手順を添えてください。
[不具合報告](https://github.com/31916/ImeLayoutRouter/issues) /
[公開時の注意・脆弱性の非公開報告](SECURITY.md)
