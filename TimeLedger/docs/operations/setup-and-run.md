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
- Secret Manager 例（`TimeLedger` プロジェクト直下で実行）  
  - `dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=...;Port=...;Database=...;Username=...;Password=..."`  
  - `dotnet user-secrets set "Authentication:Google:ClientId" "xxx"`  
  - `dotnet user-secrets set "Authentication:Google:ClientSecret" "xxx"`

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
