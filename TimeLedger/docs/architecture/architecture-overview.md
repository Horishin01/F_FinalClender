# アーキテクチャ概要（更新日: 2026-02-12）

## スタック
- ASP.NET Core 8 (MVC + Razor Pages + Identity)
- Entity Framework Core (Npgsql) / PostgreSQL
- フロントエンド: Razor + Vanilla JS + FullCalendar、スタイルは `wwwroot/css`、ベンダー資産は `wwwroot/lib`

## レイヤー構成
- **Controllers**: 画面/JSON/API を提供 (`EventsController`, `CategoriesController`, `AuthController`, `AppNoticesController` など)。
- **Services**: 業務ロジックと外部連携 (`CloudCalDavService`, `ExternalCalendarSyncService`, `OutlookCalendarService`, `GoogleCalendarService`, `IcalParserService` ほか)。
- **Data**: `ApplicationDbContext` が Identity テーブルとドメインテーブルを EF Core で管理。
- **Middleware**: `UseUserAccessLogging` でアクセスログを DB に記録。Antiforgery はヘッダー `RequestVerificationToken` を要求。
- **Views/ViewModels**: Razor ビューと対応する ViewModel DTO が `Views/` と `ViewModels/` に配置。

## 主要フロー（HTTP リクエスト→レスポンス）
1. `Program.cs` で DI/認証/ミドルウェアを構成し、既定ルート `{controller=Home}/{action=Index}` を登録。
2. クライアント → MVC コントローラー → 必要に応じてサービス経由で外部 API や DB へアクセス。
3. 返却: Razor View あるいは JSON (FullCalendar などのフロント用データ)。
4. 開発環境: `UseMigrationsEndPoint`、本番: `UseExceptionHandler + HSTS` を適用。

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

## フロントエンド構成
- FullCalendar 初期化と統合カレンダー UI は `wwwroot/js/events-integrated.js`。カテゴリ/ソース/統計などの拡張 UI を同ファイルで制御。
- テーマはグラデーション背景＋ガラス調カード。共有レイアウトは `Views/Shared/_Layout.cshtml`。
