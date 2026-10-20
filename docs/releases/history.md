# 開発履歴 / Development history

日時はGitコミットの記録を日本時間（UTC+09:00）で記載しています。
実装日時とリリース日時は同じではありません。削除された旧GitHub Releaseの
公開日時を、コミットやタグの日時から推定して掲載していません。

Dates below are Git commit timestamps in Japan time (UTC+09:00). Implementation
dates do not establish publication dates. The deleted prototype GitHub Release's
publication time is not inferred from its commit or tag.

| 日時 / Date and time (JST) | 内容 / Event | Gitの記録 / Evidence |
| --- | --- | --- |
| 2026/09/26 22:59:23 | Ver1.1.1・Ver2.0.1のインストーラーを配布サイトへ追加。ブラウザーの入力欄検出処理を修正 / Added the Ver1.1.1 and Ver2.0.1 installers with corrected browser field detection | [配布コミット / Website commit](https://github.com/31916/31916.github.io/commit/4fcf6bc59d2c7e4ad1a95cafdc50a9c931f2dbba) |
| 2026/09/20 11:02:08 | V1 1.1.0・V2 2.0.0のインストーラーを配布サイトへ追加 / Added both installers to the distribution website | [配布コミット / Website commit](https://github.com/31916/31916.github.io/commit/56d553938fcbe38631daf3435f195ee2960b94b5) |
| 2026/09/01 00:21:44 | 旧v1.0.0のタグが参照するコミット。READMEの配列説明を更新 / Commit referenced by the old v1.0.0 tag; updated the README | [9555755](https://github.com/31916/ImeLayoutRouter/commit/9555755e5fda336016c18f97542dd59b95ddcad9) |
| 2026/08/14 17:24:35 | 設定画面と通知領域への常駐機能を追加 / Added settings and the tray application | [5696377](https://github.com/31916/ImeLayoutRouter/commit/569637761e661b2b3ce5795334542932d45c5ab5) |
| 2026/08/11 17:30:19 | 初期プロトタイプの双方向切替機能を実装 / Implemented the initial bidirectional switching prototype | [5e8d912](https://github.com/31916/ImeLayoutRouter/commit/5e8d9129daa5434048cba49f291764f227d0c17e) |

Ver1.1.1・Ver2.0.1は2026年9月26日に[サイトのデプロイ](https://github.com/31916/31916.github.io/actions/runs/36247110823)が完了し、公開URLから取得した両インストーラーのSHA-256がWindows CIの成果物と一致することを確認しました。表の22:59:23は配布コミットの記録です。

Ver1.1.1 and Ver2.0.1 became available after the September 26 website deployment.
Both public installers were downloaded and their SHA-256 hashes matched the Windows
CI artifacts. The table's 22:59:23 is the distribution commit time.

V1・V2は2026年9月20日に[サイトのデプロイ](https://github.com/31916/31916.github.io/actions/runs/35482948909)が完了し、公開URLから取得したインストーラーのSHA-256を確認しました。
表の11:02:08は配布コミットの記録であり、デプロイ完了の秒単位の時刻ではありません。

Both editions became available after the September 20 website deployment; their
public downloads were verified against the published SHA-256 hashes. The table's
11:02:08 is the distribution commit time, not the exact deployment completion time.

`v1.0.0` は軽量タグで、独立したタグ作成日時を保持していません。旧プロトタイプの配布は終了しています。
The lightweight `v1.0.0` tag has no separate tag-creation timestamp. The prototype download has been retired.

[Ver1.1.1](v1-1.1.1.md) ｜ [Ver2.0.1](v2-2.0.1.md) ｜ [配布ページ / Downloads](https://31916.ch/IMELayoutRouter/)
