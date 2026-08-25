# 開発環境セットアップ & 実行手順（更新日: 2026-08-25）

## 前提
- .NET SDK 8.0.x
- Docker Desktop（Linuxコンテナ）+ Docker Compose v2。PostgreSQL 16.15はプロジェクト配下のComposeで起動する。
- Node.js 18+（FullCalendar を npm 取得する場合のみ）
- `dotnet-ef` CLI（マイグレーション適用用）: `dotnet tool install --global dotnet-ef`

## リポジトリとソリューション
- 推奨: `TimeLedger/TimeLedger.sln`（サブフォルダ直下）を開く。  
- ルート直下にも旧版 `../TimeLedger.sln` があるため、IDE でプロジェクトを重複読み込みしないよう注意。

## 設定
- 開発端末固有の設定は、プロジェクト直下の `appsettings.Development.Local.example.json` を `appsettings.Development.Local.json` へコピーして設定する。Local.jsonはGit管理外で、Developmentだけが読み込む。
- `appsettings.Development.json` は秘密情報を含まない共有設定としてGit管理する。環境変数とコマンドライン指定はLocal.jsonより優先する。
- 必須キー  
  - `ConnectionStrings:DefaultConnection`（PostgreSQL 接続文字列）  
  - `Authentication:Outlook:ClientId|ClientSecret`（利用時のみ必須）  
  - `Authentication:Google:ClientId|ClientSecret`（利用時のみ必須）
  - `DiscordNotifications:Enabled`（既定は`false`。Discord通知を利用する場合のみ`true`）
  - `DiscordNotifications:WebhookUrl`（Discord Webhook URL。環境変数またはSecret Managerでのみ設定）
- User Secretsも代替手段として利用できるが、この開発環境では `appsettings.Development.Local.json` を使用する。
- 起動前の設定確認には `dotnet run -- --validate-db-configuration` を使う。この確認は接続先へ接続せず、接続文字列の値も表示しない。
  - `dotnet user-secrets set "Authentication:Google:ClientId" "xxx"`  
  - `dotnet user-secrets set "Authentication:Google:ClientSecret" "xxx"`
  - `dotnet user-secrets set "DiscordNotifications:Enabled" "true"`
  - `dotnet user-secrets set "DiscordNotifications:WebhookUrl" "https://discord.com/api/webhooks/..."`
- 開発用Adminを自動作成する必要がある場合だけ、Local.jsonに `BootstrapAdmin:Enabled=true`、`Email`、`Password` を設定する。ProductionではLocal.jsonを読み込まず、この機能も起動前に拒否する。

### Discord予定リマインダー

- `Event.ReminderMinutesBefore` が設定された単発・時間指定予定だけを対象に、開始前にDiscord Webhookへ通知する。
- 既定のポーリング間隔は60秒。必要なら`DiscordNotifications:PollIntervalSeconds`を15〜300秒で指定する。
- 同じ予定・同じ通知時刻の送信記録をDBに保存して、アプリ再起動後も重複送信を避ける。
- 終日予定、繰り返し予定、ブラウザーのlocalStorageだけに存在するFlowログは通知対象外。期限付きタスク通知やDiscord BotのDM・コマンド連携は未実装。

## DB 準備
1) 既存DBを引き継ぐ場合は `./database/scripts/Migrate-DevelopmentDatabase.ps1` を実行する。バックアップ・復元・疎通に成功した場合だけLocal設定が `127.0.0.1:55432` へ切り替わる。
2) 新規DBの場合は `database/config/development.env.example` をGit管理外の `development.env` へコピーし、例示パスワードを変更する。
3) `./database/scripts/Database.ps1 -Environment Development -Action Start` で起動する。DB本体は `database/runtime/development/data` に保持される。
4) ルートで `dotnet restore`。
5) `dotnet ef database update --project TimeLedger.csproj --startup-project TimeLedger.csproj` で最新マイグレーションを適用。

詳細とバックアップ・本番移行は `database/README.md` を参照する。

## 実行
- 開発: `dotnet watch run --project TimeLedger/TimeLedger.csproj`  
- 通常: `dotnet run --project TimeLedger/TimeLedger.csproj`  
- 既定URL: `http://localhost:5016`。Visual Studio / VS CodeのF5起動は `http` Projectプロファイルだけを使用する。IIS Expressプロファイルは用意せず、VS Codeでは `checkForDevCert=false` として開発証明書の確認・作成・信頼を行わない。ブラウザーはHTTP待受開始後に自動起動する。

## 初期アカウント
- 初期Adminの自動作成は既定で無効。DevelopmentでUser Secretsに明示設定した場合だけ実行する。
- 既存DBに旧固定資格情報から作られたAdminがある場合は、公開前にパスワード変更またはアカウント無効化を行う。

## ビルド/公開
- `dotnet publish -c Release -o ./publish`  
- 公開先で `ConnectionStrings__DefaultConnection` などを環境変数に設定し、`ASPNETCORE_ENVIRONMENT=Production` で起動する。
- ProductionはNginxでTLS終端し、Kestrelは `127.0.0.1:5016` のHTTPだけを使用する。アプリへ証明書・秘密鍵を渡さない。
- 公開前に `ASPNETCORE_ENVIRONMENT=Production AllowedHosts=<実ドメイン> dotnet TimeLedger.dll --validate-web-security` と `dotnet TimeLedger.dll --validate-db-configuration` を実行する。どちらの検査もDB接続やAdmin作成を行わない。

## フロントエンド
- `wwwroot/lib` にベンダー資産は同梱済み。npm から再取得する場合はリポジトリ直下で `npm install`（`fullcalendar@^6.1.15`）。

## GitHub正式リリース

- 通常の工程許可では、検証済み変更を現在の作業ブランチへpushするところまで行う。
- 利用者が本案件の正式リリースを明示的に許可した場合だけ、mainとの競合、CI、文書、秘密情報、自動デプロイ影響を再確認してmainへ通常マージまたはPRで反映する。
- GitHub上のmainに対象コミットが含まれることを確認してからremote作業ブランチを削除し、ローカルmainをfast-forwardで同期してからローカル作業ブランチを削除する。
- main反映を確認できない場合はブランチを削除しない。force push、履歴改変は禁止し、GitHub Release、タグ、本番公開は別の明示許可を必要とする。
