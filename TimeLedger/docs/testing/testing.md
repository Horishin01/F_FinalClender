# テスト方針（更新日: 2026-08-25）

## 現状
- 自動テストプロジェクトは未整備。
- 出荷前は手動回帰テストを実施し、最低限の認可テストを必須化する。

## アプリ別手動チェックリスト
- `Home / Privacy / Flow`
- 匿名アクセスで表示可能な内容のみ表示されること。
- Flow の記録がブラウザローカル保存で完結し、サーバー保存されないこと。

- `Events（カレンダー）`
- ログインユーザーのイベントのみ表示・編集・削除できること。
- 他ユーザーのイベント ID を指定しても参照/更新/削除できないこと。
- `Sync` が 60 秒レート制限とエラーハンドリングを維持していること。

- `Discord予定リマインダー`
- `DiscordNotifications:Enabled=false` では外部送信が行われないこと。
- 有効時に、単発かつ時間指定で`ReminderMinutesBefore`を設定した予定だけが通知候補になること。
- 終日予定・繰り返し予定・リマインダー未設定予定が通知されないこと。
- 同じ予定・同じ通知時刻を再実行しても、`DiscordNotificationDeliveries`の一意制約により二重送信されないこと。
- 送信失敗時は送信記録が残らず、次回ポーリングで再試行できること。

- `Categories`
- CRUD が自ユーザー範囲で完結すること。
- 他ユーザーカテゴリ ID 指定で更新/削除できないこと。
- 使用中カテゴリ削除が拒否されること。

- `外部連携（Outlook/Google/iCloud）`
- 連携開始、再連携、解除、期限切れ時の再認証誘導が成立すること。
- 未連携状態で同期実行時に適切な案内画面へ遷移すること。
- iCloud 設定保存・更新・削除が管理者のみ実行可能であること。

- `ICカード`
- 管理者のみ登録/削除できること。
- 一般ユーザーは閲覧のみで操作不可であること。
- `pcsc-lite` 未導入時に案内メッセージが表示されること。

- `管理アプリ（Admin/Analytics/AppNotices/Users/Roles/UserRoles/Tools）`
- 匿名/一般ユーザーはアクセス拒否されること。
- 管理者は必要機能にアクセスできること。
- ユーザー管理・ロール管理の変更が期待通り反映されること。

## セキュリティ重点テスト
- 認可マトリクス試験（匿名/一般/管理者）を主要 URL に対して実施。
- CSRF トークンなし POST が拒否されること。
- 入力異常時に 500 を返さず、機密情報を画面表示しないこと。
- ログにトークン/パスワードが出力されないこと。

## HTTP・HTTPS境界の回帰確認
- Windowsでは `pwsh -File ./TimeLedger/動作確認/HTTPS構成確認.ps1` を実行すると、Releaseビルドと次のDB非接続10ケースをまとめて確認できる。
- Developmentの起動プロファイルがKestrel用の `http` 1件だけで、IIS Express設定やHTTPS URLを含まないこと。VS CodeのF5構成が `checkForDevCert=false` を明示し、`http://localhost:5016` のブラウザー自動起動を維持しながら開発証明書警告を表示しないこと。
- `--validate-web-security` がDevelopmentでは成功すること。
- Productionで `Security:RequireHttps=false`、`AllowedHosts=*` または未設定、loopback以外の `TrustedProxyIp`、`BootstrapAdmin:Enabled=true` の各条件を拒否すること。
- Productionで実ホスト名を設定した構成検査がDB接続なしで成功すること。
- `--validate-db-configuration` がDevelopmentおよびProductionで未設定の `ConnectionStrings:DefaultConnection` を環境別エラーコードで拒否し、ダミー値ではDBへ接続せず成功すること。
- Nginxの `nginx -t`、HTTP 308、HTTPS応答、証明書SAN・発行元・期限・チェーン、HSTS、Certbot更新dry-runを実機で確認すること。
- Kestrelの5016がloopback限定で、転送ヘッダーなしの直接HTTP要求を拒否すること。任意送信元の転送ヘッダーを信頼する設定へ緩和しない。
- 実ドメイン・証明書がない場合はProduction HTTPSの実機項目を未確認として残し、テストHTTP成功で代用しない。

## 自動化の優先順
1. 認可・所有者照合の統合テスト（`WebApplicationFactory`）。
2. サービス層のユニットテスト（`ExternalCalendarSyncService`, `OutlookCalendarService`, `GoogleCalendarService`, `CloudCalDavService`）。
3. DB 統合テスト（Testcontainers PostgreSQL）。

## 実行コマンド（テスト追加後）
- `dotnet test`
