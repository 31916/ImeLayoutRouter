# IMELayoutRouter 数字欄の判定 / Numeric fields

## 日本語

Chrome・Edgeが通常のテキスト欄としてWindowsに伝える数字欄を、V1・V2で判定するための任意の拡張機能です。

1. インストーラーの追加タスクで「Chrome・Edge用の数字欄拡張のファイルを導入する」を選びます。V1またはV2を起動し、日本語IMEと切替先を設定します。
2. Chromeでは `chrome://extensions`、Edgeでは `edge://extensions` を開きます。「デベロッパー モード」を有効にして「パッケージ化されていない拡張機能を読み込む」を選び、インストール先の `browser-extension` フォルダーを指定します。既定の場所は `%LOCALAPPDATA%\Programs\IME Layout Router V1\browser-extension` または `%LOCALAPPDATA%\Programs\IME Layout Router V2\browser-extension` です。
3. 対象サイトで拡張機能のボタンを押すと、設定用のタブが開きます。表示されたサイト名を確認して「このサイトで有効にする」を選び、ブラウザーの許可画面で承認します。「このサイトでは有効です」と表示されたら「サイトへ戻る」を押し、数字欄を選びます。
4. 同じボタンから「このサイトで無効にする」を選ぶと解除できます。拡張自体の削除は、ブラウザーの拡張機能ページで行えます。

拡張を読み込んだ版を削除して別の版に切り替える場合は、残す版の `browser-extension` フォルダーから拡張を読み込み、必要に応じてサイトを再び有効にしてください。上書き時にファイル使用中と表示された場合は、拡張を一時的に無効にして更新してください。

許可の対象はプロトコル・ホスト・ポートが一致するサイトです。Chrome・Edgeのホスト権限は同じホストの複数ポートに及びますが、拡張は有効にしたポートでだけ判定します。別サイトの埋め込み欄は、外側と埋め込み側の両方を許可した場合に限り判定対象になります。

有効なサイトがある間はWindowsアプリとの接続を保持します。入力欄の判定は許可したサイトのタブが前面にあるときだけ行い、最後のサイトを無効にすると接続も閉じます。

数字・小数用の入力モード、数値欄、`[0-9]+` や `\d{6}` のように数字だけを要求する単純な形式に対応します。サイト独自のJavaScriptだけで制限する欄は対象外です。ブラウザーからWindowsアプリに返すのは数字欄かどうかの判定だけです。入力した文字や認証コードを読み取らず、URLやページタイトルもアプリへ送信しません。

## English

This optional Chrome and Edge extension helps V1 and V2 recognize numeric fields that Windows sees as ordinary text fields.

1. In the installer, select **Install the optional Chrome / Edge numeric field companion files**. Start V1 or V2 and configure the source IME and target layout.
2. Open `chrome://extensions` or `edge://extensions`, turn on **Developer mode**, select **Load unpacked**, and choose the installed `browser-extension` folder. The default location is `%LOCALAPPDATA%\Programs\IME Layout Router V1\browser-extension` or `%LOCALAPPDATA%\Programs\IME Layout Router V2\browser-extension`.
3. Open the site you want to use and click the extension button to open its setup tab. Check the site shown and choose **Enable on this site**. Approve the browser's permission prompt. Once **Enabled on this site** appears, choose **Back to site** and focus the numeric field.
4. Use **Disable on this site** to revoke access. You can remove the extension from the browser's extensions page.

If you remove the edition whose extension folder you loaded and switch to another edition, load its `browser-extension` folder and enable your sites again if needed. If an upgrade reports files in use, temporarily disable the extension during that upgrade.

Enrollment applies only to the scheme, host and port you chose. The browser grants host permission across ports, but the extension checks the exact origin. For embedded fields, both the containing site and the embedded origin must be enrolled.

The connection to the Windows app stays open while a site is enrolled. Field checks run only in the foreground tab on a permitted site. Disabling the last site closes the connection.

The extension recognizes number controls, numeric and decimal input modes, and simple digit-only patterns such as `[0-9]+` and `\d{6}`. JavaScript-only restrictions are outside its scope. It sends only the field classification to the Windows app. It never reads typed characters or verification codes or sends page URLs or titles to the app.
