# 開発履歴 / Development history

日時はGitコミットの記録を日本時間（UTC+09:00）で記載しています。
実装日時とリリース日時は同じではありません。削除された旧GitHub Releaseの
公開日時を、コミットやタグの日時から推定して掲載していません。

Dates below are Git commit timestamps in Japan time (UTC+09:00). Implementation
dates do not establish publication dates. The deleted prototype GitHub Release's
publication time is not inferred from its commit or tag.

| 日時 / Date and time (JST) | 内容 / Event | Gitの記録 / Evidence |
| --- | --- | --- |
| 2026/09/20 11:02:08 | V1 1.1.0・V2 2.0.0のインストーラーを配布サイトへ追加 / Added both installers to the distribution website | [配布コミット / Website commit](https://github.com/31916/31916.github.io/commit/0a0c9c9c6621a1c06e84037048f11ca867ca39e7) |
| 2026/09/01 00:21:44 | 旧v1.0.0のタグが参照するコミット。READMEの配列説明を更新 / Commit referenced by the old v1.0.0 tag; updated the README | [58b21dd](https://github.com/31916/ImeLayoutRouter/commit/58b21dd816639573c91f8c76864a0656c0c62d9a) |
| 2026/08/14 17:24:35 | 設定画面と通知領域への常駐機能を追加 / Added settings and the tray application | [94c569d](https://github.com/31916/ImeLayoutRouter/commit/94c569d220f47fd3ece2a1203e3f3932275351f5) |
| 2026/08/11 17:30:19 | 初期プロトタイプの双方向切替機能を実装 / Implemented the initial bidirectional switching prototype | [6472d7e](https://github.com/31916/ImeLayoutRouter/commit/6472d7ed9215dcde28d939c78cefd1de69c7e354) |

V1・V2は2026年9月20日に[サイトのデプロイ](https://github.com/31916/31916.github.io/actions/runs/35482948909)が完了し、公開URLから取得したインストーラーのSHA-256を確認しました。
表の11:02:08は配布コミットの記録であり、デプロイ完了の秒単位の時刻ではありません。

Both editions became available after the September 20 website deployment; their
public downloads were verified against the published SHA-256 hashes. The table's
11:02:08 is the distribution commit time, not the exact deployment completion time.

`v1.0.0` は軽量タグで、独立したタグ作成日時を保持していません。旧プロトタイプの配布は終了しています。
The lightweight `v1.0.0` tag has no separate tag-creation timestamp. The prototype download has been retired.

[V1 1.1.0](v1-1.1.0.md) ｜ [V2 2.0.0](v2-2.0.0.md) ｜ [配布ページ / Downloads](https://31916.ch/IMELayOutRouter/)
