# アーキテクチャ概要（更新日: 2026-08-25）

## スタック
- ASP.NET Core 8 (MVC + Razor Pages + Identity)
- Entity Framework Core (Npgsql) / PostgreSQL
- フロントエンド: Razor + Vanilla JS + FullCalendar、スタイルは `wwwroot/css`、ベンダー資産は `wwwroot/lib`

## レイヤー構成
- **Controllers**: 画面/JSON/API を提供 (`EventsController`, `CategoriesController`, `AuthController`, `AppNoticesController` など)。
- **Services**: 業務ロジックと外部連携 (`CloudCalDavService`, `ExternalCalendarSyncService`, `OutlookCalendarService`, `GoogleCalendarService`, `IcalParserService` ほか)。`DiscordReminderWorker` は予定リマインダーを定期確認するバックグラウンドサービス。
- **Data**: `ApplicationDbContext` が Identity テーブルとドメインテーブルを EF Core で管理。
- **Middleware**: `UseUserAccessLogging` でアクセスログを DB に記録。Antiforgery はヘッダー `RequestVerificationToken` を要求。
- **Views/ViewModels**: Razor ビューと対応する ViewModel DTO が `Views/` と `ViewModels/` に配置。

## 主要フロー（HTTP リクエスト→レスポンス）
1. DevelopmentはクライアントからKestrelの `http://localhost:5016` へ直接接続する。
2. ProductionはクライアントHTTPS → Nginx（証明書/TLS終端）→ loopback HTTPのKestrelという経路だけを使用する。
3. `Program.cs` は既知loopbackプロキシの転送ヘッダーを先に処理し、HTTPSと確認できないProduction要求を拒否してから、MVC/Razor/認証ミドルウェアへ渡す。
4. クライアント → MVC コントローラー → 必要に応じてサービス経由で外部 API や DB へアクセス。
5. 返却: Razor View あるいは JSON (FullCalendar などのフロント用データ)。Developmentは `UseMigrationsEndPoint`、Productionは `UseExceptionHandler + HSTS` を適用。

Nginxの設定例は `deploy/nginx/` に置く。証明書と秘密鍵は `/etc/letsencrypt` 等の本番基盤で管理し、リポジトリへ含めない。

## 認証・認可
- Identity (Email 確認必須) + ロール。管理系はロールガードを付与する想定。
- 外部 OAuth: Outlook / Google (スコープはカレンダー操作用)。ClientId/Secret が設定されている場合のみ有効化。
- iCloud: CalDAV (ユーザー入力の Apple ID + アプリパスワード) を `ICloudSetting` に保存し、サービスが利用。

## データ永続化
- PostgreSQL。接続文字列は `ConnectionStrings:DefaultConnection`。
- タイムゾーン既定値は `Calendar:DefaultTimeZoneId = Asia/Tokyo`（クライアント/サーバー双方で一致させる）。
- マイグレーションは `Migrations/` に保存し、`dotnet ef database update` で適用。

## キャッシュ・非機能
- `IMemoryCache` を CalDAV 同期のクールダウン管理に使用。
- 例外は開発で詳細、運用で一般エラーページを返す。静的ファイルは `FileExtensionContentTypeProvider` で MIME を拡張。

## 外部連携
- **CalDAV (iCloud)**: 予定の取得・作成・更新・削除に対応。UID ベースで DB と iCloud を同期（更新/削除の衝突処理は TODO）。
- **Outlook/Google カレンダー**: OAuth トークンを DB に保持し、`ExternalCalendarSyncService` がイベントを upsert（暗号化は今後の課題）。
- **Discord Webhook**: `DiscordNotifications:Enabled=true` の時だけ、単発・時間指定予定の既存リマインダーをWebhookへ送信する。Webhook URLは環境変数またはSecret Managerだけで設定し、appsettingsやDBには保存しない。終日予定、繰り返し予定、Flowログは対象外。

## フロントエンド構成
- FullCalendar 初期化と統合カレンダー UI は `wwwroot/js/events-integrated.js`。カテゴリ/ソース/統計などの拡張 UI を同ファイルで制御。
- テーマはグラデーション背景＋ガラス調カード。共有レイアウトは `Views/Shared/_Layout.cshtml`。
