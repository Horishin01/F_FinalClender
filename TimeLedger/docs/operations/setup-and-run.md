# 開発環境セットアップ & 実行手順（更新日: 2026-02-12）

## 前提
- .NET SDK 8.0.x
- PostgreSQL 14+（ローカルまたは接続可能な環境）
- Node.js 18+（FullCalendar を npm 取得する場合のみ）
- `dotnet-ef` CLI（マイグレーション適用用）: `dotnet tool install --global dotnet-ef`

## リポジトリとソリューション
- 推奨: `TimeLedger/TimeLedger.sln`（サブフォルダ直下）を開く。  
- ルート直下にも旧版 `../TimeLedger.sln` があるため、IDE でプロジェクトを重複読み込みしないよう注意。

## 設定
- ローカルは `appsettings.Development.json` を編集するか、環境変数/Secret Manager で上書きする。
- 必須キー  
  - `ConnectionStrings:DefaultConnection`（PostgreSQL 接続文字列）  
  - `Authentication:Outlook:ClientId|ClientSecret`（利用時のみ必須）  
  - `Authentication:Google:ClientId|ClientSecret`（利用時のみ必須）
  - `DiscordNotifications:Enabled`（既定は`false`。Discord通知を利用する場合のみ`true`）
  - `DiscordNotifications:WebhookUrl`（Discord Webhook URL。環境変数またはSecret Managerでのみ設定）
- Secret Manager 例（`TimeLedger` プロジェクト直下で実行）  
  - `dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=...;Port=...;Database=...;Username=...;Password=..."`  
  - `dotnet user-secrets set "Authentication:Google:ClientId" "xxx"`  
  - `dotnet user-secrets set "Authentication:Google:ClientSecret" "xxx"`
  - `dotnet user-secrets set "DiscordNotifications:Enabled" "true"`
  - `dotnet user-secrets set "DiscordNotifications:WebhookUrl" "https://discord.com/api/webhooks/..."`

### Discord予定リマインダー

- `Event.ReminderMinutesBefore` が設定された単発・時間指定予定だけを対象に、開始前にDiscord Webhookへ通知する。
- 既定のポーリング間隔は60秒。必要なら`DiscordNotifications:PollIntervalSeconds`を15〜300秒で指定する。
- 同じ予定・同じ通知時刻の送信記録をDBに保存して、アプリ再起動後も重複送信を避ける。
- 終日予定、繰り返し予定、ブラウザーのlocalStorageだけに存在するFlowログは通知対象外。期限付きタスク通知やDiscord BotのDM・コマンド連携は未実装。

## DB 準備
1) PostgreSQL で DB とユーザーを作成。  
2) ルートで `dotnet restore`。  
3) `dotnet ef database update --project TimeLedger/TimeLedger.csproj` で最新マイグレーションを適用。

## 実行
- 開発: `dotnet watch run --project TimeLedger/TimeLedger.csproj`  
- 通常: `dotnet run --project TimeLedger/TimeLedger.csproj`  
- 既定 URL: `https://localhost:7052` / `http://localhost:5016`（`launchSettings.json` 依存）

## 初期アカウント
- 起動時に Admin ユーザー `admin@admin.admin` がシードされ、パスワードは `i2JvwXGn<>`（開発用）。本番前に必ず変更またはシード処理を修正すること。

## ビルド/公開
- `dotnet publish -c Release -o ./publish`  
- 公開先で `ConnectionStrings__DefaultConnection` などを環境変数に設定し、`ASPNETCORE_ENVIRONMENT=Production` で起動する。

## フロントエンド
- `wwwroot/lib` にベンダー資産は同梱済み。npm から再取得する場合はリポジトリ直下で `npm install`（`fullcalendar@^6.1.15`）。
