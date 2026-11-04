# IMELayoutRouter 数字欄の判定 / Numeric fields

## 日本語

Chrome・Edgeが通常のテキスト欄としてWindowsに伝える数字欄を、V1・V2で判定するための任意の拡張機能です。

1. インストーラーの追加タスクで「Chrome・Edge用の数字欄拡張のファイルを導入する」を選びます。V1またはV2を起動し、日本語IMEと切替先を設定します。
2. Chromeでは `chrome://extensions`、Edgeでは `edge://extensions` を開きます。「デベロッパー モード」を有効にして「パッケージ化されていない拡張機能を読み込む」を選び、インストール先の `browser-extension` フォルダーを指定します。既定の場所は `%LOCALAPPDATA%\Programs\IME Layout Router V1\browser-extension` または `%LOCALAPPDATA%\Programs\IME Layout Router V2\browser-extension` です。
3. 対象サイトを開き、拡張機能のボタンを押します。表示されたサイト名を確認して「このサイトで有効にする」を選び、ブラウザーの許可画面で承認します。拡張の画面を閉じて入力欄を選びます。
4. 同じボタンから「このサイトで無効にする」を選ぶと解除できます。拡張自体の削除は、ブラウザーの拡張機能ページで行えます。

許可の対象はプロトコル・ホスト・ポートが一致するサイトです。Chrome・Edgeのホスト権限は同じホストの複数ポートに及びますが、拡張は有効にしたポートでだけ判定します。別サイトの埋め込み欄は、外側と埋め込み側の両方を許可した場合に限り判定対象になります。

Windowsアプリとの接続は、許可したサイトのタブが前面にある間だけ開きます。別のアプリや許可していないタブに移ると閉じます。

数字・小数用の入力モード、数値欄、`[0-9]+` や `\d{6}` のように数字だけを要求する単純な形式に対応します。サイト独自のJavaScriptだけで制限する欄は対象外です。ブラウザーからWindowsアプリに返すのは数字欄かどうかの判定だけです。入力した文字や認証コードを読み取らず、URLやページタイトルもアプリへ送信しません。

## English

This optional Chrome and Edge extension helps V1 and V2 recognize numeric fields that Windows sees as ordinary text fields.

1. In the installer, select **Install the optional Chrome / Edge numeric field companion files**. Start V1 or V2 and configure the source IME and target layout.
2. Open `chrome://extensions` or `edge://extensions`, turn on **Developer mode**, select **Load unpacked**, and choose the installed `browser-extension` folder. The default location is `%LOCALAPPDATA%\Programs\IME Layout Router V1\browser-extension` or `%LOCALAPPDATA%\Programs\IME Layout Router V2\browser-extension`.
3. Open the site you want to use, click the extension button, check the site shown, and choose **Enable on this site**. Approve the browser's permission prompt, close the extension panel, and focus the field.
4. Use **Disable on this site** to revoke access. You can remove the extension from the browser's extensions page.

Enrollment applies only to the scheme, host and port you chose. The browser grants host permission across ports, but the extension checks the exact origin. For embedded fields, both the containing site and the embedded origin must be enrolled.

The connection to the Windows app stays open only while an enrolled tab is active in the foreground browser. Switching to another application or an unenrolled tab closes it.

The extension recognizes number controls, numeric and decimal input modes, and simple digit-only patterns such as `[0-9]+` and `\d{6}`. JavaScript-only restrictions are outside its scope. It sends only the field classification to the Windows app. It never reads typed characters or verification codes or sends page URLs or titles to the app.
