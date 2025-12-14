# TimeLedger システム仕様書

_最終更新: 2026-03-13。仕様書とコードコメントは日本語を基本言語とする。挙動・ルート・データモデル・フローを変更した場合は、本書も同じコミットで更新すること。_

## 1. 目的と適用範囲
- iCloud (CalDAV) の予定をユーザー単位で取得し、FullCalendar 上で可視化しながら PostgreSQL に保存・編集できる Web アプリを提供する。
- Visual Studio / VS Code どちらからでも同等の操作ができるよう、ユーザー管理・ロール管理・割り当て画面を備える。
- 各ユーザーの iCloud 認証情報を保持し、認証済み同期ジョブを実行できる仕組みを維持する。

## 2. アーキテクチャ概要
- **プラットフォーム:** ASP.NET Core 8 (MVC + Razor Pages、Identity 領域付き)。
- **言語 / ターゲット:** C# 12、.NET 8.0 (`TimeLedger.csproj`)。
- **主要ライブラリ:** ASP.NET Core Identity、Entity Framework Core (Npgsql)、Ical.Net、PCSC/PCSC.Iso7816、Microsoft.VisualStudio.Web.CodeGeneration.Design など。
- **フロントエンド:** FullCalendar を `wwwroot/js/events-integrated.js` で初期化し、素の JavaScript で UI 操作を実装。
- **ホスティング:** Kestrel。デバッグ時は `https://localhost:7052;http://localhost:5016` を使用。

## 3. 実行時構成 (Program.cs)
1. `DefaultConnection` を構成ファイルから読み込み (未設定時は例外)。
2. `ApplicationDbContext` を Npgsql プロバイダーで登録。
3. Identity を設定 (`ApplicationUser` + Roles、メール確認必須、Razor Pages / MVCを登録)。
4. Microsoft / Google OAuth を外部スキームとして追加（`CalendarAuthDefaults.OutlookScheme` / `GoogleScheme`、`SaveTokens=true`、スコープは Calendars.ReadWrite / Google Calendar）。※実値は `appsettings` から取得する TODO コメント付き。
5. `AddHttpContextAccessor`、`ICloudCalDavService`、`IcalParserService`、外部カレンダー用 `OutlookCalendarService` / `GoogleCalendarService` / `ExternalCalendarSyncService`、`AddMemoryCache`、`AddAntiforgery`(ヘッダー `RequestVerificationToken`) を DI へ追加。
6. パイプライン: Development=`UseMigrationsEndPoint`、Production=`UseExceptionHandler("/Home/Error")`+`UseHsts()`、共通=HTTPS 強制/静的ファイル/StatusCodePages/Routing/Authentication/Authorization、既定ルート `{controller=Home}/{action=Index}/{id?}/{id2?}` + Razor Pages。

## 4. 設定と環境
- `appsettings.json`: PostgreSQL 接続文字列 (Host `192.168.1.104`, DB `pit2_hi022052`)。本番は Secret Manager / KeyVault で秘匿する。
- `appsettings.Development.json`: ログレベルのみ上書き。接続文字列はデフォルトを利用。
- `Properties/launchSettings.json` と `.vscode/launch.json` で VS / VS Code 双方向けの起動プロファイルを用意。
- 主要環境変数: `ASPNETCORE_ENVIRONMENT` / `DOTNET_ENVIRONMENT` (通常 `Development`)、VS Code デバッグ時の `ASPNETCORE_URLS=https://localhost:7052;http://localhost:5016`。

## 5. 認証・認可
- ASP.NET Core Identity (EF Core) を利用し、`AspNetUsers` 等の標準テーブルでユーザー・ロールを管理。
- `ApplicationUser` (`Models/ApplicationUser.cs`) は `IdentityUser` を継承し、各エンティティで外部キーとして参照。
- ロール (`IdentityRole`) とユーザー/ロール紐付け (`IdentityUserRole`) は専用コントローラーで CRUD 操作可能。
- 管理系コントローラーの多くは `[Authorize]` がコメントアウトされているため、本番前に役割ごとのガードを復活させること。
- アカウント設定の「個人データの削除」はユーザー削除完了後に `ApplicationDbContext.Database.EnsureDeletedAsync()` を実行し、データベースごと削除する。

## 6. ドメインモデル概要
| エンティティ | 主なフィールド | 目的 / メモ |
| --- | --- | --- |
| `Event` (`Models/Event.cs`) | `Id`, `UserId`, `UID?`, `Title`, `StartDate`, `EndDate`, `LastModified`, `Description?`, `AllDay` (+ UI 用 `IsAllDay`)、`Source`,`CategoryId`(FK→`CalendarCategory`),`Priority`,`Location`,`AttendeesCsv`,`Recurrence`,`ReminderMinutesBefore`,`RecurrenceExceptions?` | カレンダー予定。外部同期用 UID は null 許容で、ローカル作成のイベントは UID なしで保存可。カテゴリはマスタ参照型に変更し、色/アイコンをカテゴリ側で管理。繰り返しはビュー範囲内で UI 展開し、リマインダーとともにバッジ表示する（DB保存は1件のまま、実通知計算は未実装）。`RecurrenceExceptions` に日付(yyyy-MM-dd)を保持して単発スキップを表現する。 |
| `CalendarCategory` (`Models/CalendarCategory.cs`) | `Id`, `UserId`, `Name`, `Icon`, `Color` | ユーザーごとのカテゴリマスタ。アイコン(FontAwesome)とカラーを任意設定可。`CategoriesController` で CRUD。Events から FK 参照。 |
| `OutlookCalendarConnection` (`Models/OutlookCalendarConnection.cs`) | `Id`, `UserId`, `Provider="Outlook"`, `AccountEmail`, `AccessTokenEncrypted`, `RefreshTokenEncrypted?`, `ExpiresAtUtc?`, `Scope?`, `LastSyncedAtUtc?`, `CreatedAtUtc`, `UpdatedAtUtc` | Outlook カレンダー用の認可コードフローで取得したトークンをサーバー側が保持。UserId にユニークインデックスがあり 1:1。列名は Encrypted だが現状は TODO でプレーン保存のため、公開前に暗号化と鍵管理を実装する。ユーザーは手入力しない。 |
| `GoogleCalendarConnection` (`Models/GoogleCalendarConnection.cs`) | `Id`, `UserId`, `Provider="Google"`, `AccountEmail`, `AccessTokenEncrypted`, `RefreshTokenEncrypted?`, `ExpiresAtUtc?`, `Scope?`, `LastSyncedAtUtc?`, `CreatedAtUtc`, `UpdatedAtUtc` | Google Calendar 用トークンストア。UserId にユニークインデックスがあり 1:1。Outlook と同様に OAuth 認可コードフローでサーバーが保持し、リフレッシュ/同期状態を管理（暗号化は TODO）。 |
| `ExternalCalendarAccount` (`Models/ExternalCalendarAccount.cs`) | (legacy) `Id`, `UserId`, `Provider`, `AccountEmail`, `AccessToken`, `RefreshToken?`, `ExpiresAt?`, `Scope?`, `CreatedAt`, `UpdatedAt` | 旧テーブル。トークン手貼り付け用の暫定実装として残置。新設の接続テーブルへ段階的に移行する。 |
| `ICloudSetting` (`Models/ICloudSetting.cs`) | `Id`(GUID文字列), `UserId`, `Username`, `Password` | iCloud 認証情報。現在は平文保存のため暗号化対応が今後の課題。 |
| `ICCard` (`Models/ICCard.cs`) | `Id`, `UserId`, `Uid` | IC カード UID とユーザーの紐付け用。現状 UI からは未使用。 |
| `AppNotice` (`Models/AppNotice.cs`) | `Id`, `Kind(Update/Incident)`, `Version?`, `Title`, `Description?`, `Highlights?`, `OccurredAt`, `ResolvedAt?`, `Status?`, `CreatedAtUtc` | プライバシーポリシーに表示するアップデート/障害情報。Admin ロールが `/AppNotices` から CRUD できる。 |
| Identity テーブル | `AspNetUsers`, `AspNetRoles`, `AspNetUserRoles` など | マイグレーション (`Migrations/*`) で生成。 |

## 7. サービスと外部連携
- **CloudCalDavService (`Services/CloudCalDavService.cs`):**
  - `/.well-known/caldav` → `calendar-home-set` → `REPORT` の順で CalDAV から予定を取得し、IcalParserService で ICS を解析後 `Events` に保存。
  - `GetAllEventsAsync` は新規 UID のみ DB へ保存（取り込み側の更新/削除反映は今後の課題）。処理件数 (scanned/saved) を返却。60 秒に 1 回のレート制限は `IMemoryCache` で実装。
  - `UpsertEventAsync` / `DeleteEventAsync` を追加。CalDAV 上で UID を検索し、存在すれば ETag を取得して `If-Match` 条件付き PUT/DELETE、無ければ `<uid>.ics` を新規作成（`If-None-Match: *`）する。更新時は `LAST-MODIFIED`/`DTSTAMP`/`SEQUENCE`（UnixTime秒ベースで増加）を付与し、CRLF 区切りで送信。`If-Match` 不一致は1回だけ条件なし PUT でフォールバック。
  - `/Events/Sync` 呼び出し時は CSRF 対策ヘッダー `RequestVerificationToken` を必須化。
- **IcalParserService (`Services/IcalParserService.cs`):**
  - Ical.Net を用いて ICS 文字列を `Event` リストへ変換。UID 重複排除、終日判定、最終更新日時を付与。取り込み時の `Source`/`Category`/`Priority`/`Location` などの拡張メタは設定していない（UI ではデフォルト値で表示）。
- **OutlookCalendarService / GoogleCalendarService (`Services/*CalendarService.cs`):**
  - OAuth2 認可コードフローで保存されたアクセストークン/リフレッシュトークン/有効期限/スコープを取得・更新・保存。`EnsureValidAccessTokenAsync` で期限切れならリフレッシュ。将来暗号化を前提としたプレーン保存で TODO コメントを残置。
  - `UpdateLastSyncedAtAsync` で同期日時を記録。`RemoveConnectionAsync` はユーザー単位で連携解除。
- **ExternalCalendarSyncService (`Services/ExternalCalendarSyncService.cs`):**
  - `IExternalCalendarClient` (Outlook/Google stub) を経由して外部予定を取得し、`Events` に upsert。取得ゼロ件でも最終同期日時を更新。
- **OAuth フロー (`Controllers/AuthController.cs`):**
  - `GET /auth/outlook/connect` → `Challenge` で Microsoft (v2.0) へリダイレクト（`offline_access Calendars.ReadWrite User.Read`）。
  - `GET /auth/outlook/callback` で `IdentityConstants.ExternalScheme` のトークンを取得・保存後、管理画面へリダイレクト。
  - `GET /auth/google/calendar/connect` → Google 認可エンドポイントへリダイレクト（`https://www.googleapis.com/auth/calendar` + `offline`）。
  - `GET /auth/google/calendar/callback` でトークン保存。いずれもユーザーはトークンを手入力しない。
- **PCSC ライブラリ:** `PCSC` / `PCSC.Iso7816` を参照済み。IC カード機能を実装する際は要件とセキュリティを別途整理する。

## 8. コントローラーと機能
### 8.1 `EventsController`
- `Index`: ログインユーザーの予定一覧。FullCalendar 用 JSON (`GetEvents`) と CalDAV 同期 (`Sync`: 60 秒クールダウン、結果 JSON) を提供。JSON は拡張メタ（ソース/カテゴリ/優先度など）を extendedProps として返す。カテゴリはマスタ参照で名前/色/アイコンを含めて返却。
- `Sync`: CalDAV から新規 UID だけを追加（取り込み側の更新/削除は未対応）。戻り値は { saved, scanned, durationMs }。
- `Create` / `Edit` / `Delete` / `Details`: GUID ID による CRUD。`Create` は `startDate`/`endDate` をクエリで受け取り初期値をセット。`Edit` は同時更新例外をハンドリング。`Source=ICloud` または `UID` 保持時は保存後に `ICloudCalDavService.UpsertEventAsync` / `DeleteEventAsync` で iCloud へ書き戻し、PUT/DELETE 成否に応じて UID を保存またはステータスメッセージを表示する。
### 8.2 `CategoriesController`
- カテゴリマスタの CRUD（ユーザー単位）。初回アクセスでデフォルトカテゴリ（仕事/会議/プライベート/締切/学習）をシード（UserId を付与）。FontAwesome アイコンとカラーコードを選択可。削除時、使用中カテゴリは削除不可（イベント再割当てが必要）。
### 8.3 `AuthController`
- `/auth/outlook/connect` / `/auth/outlook/callback` / `/auth/google/calendar/connect` / `/auth/google/calendar/callback` を提供。`Challenge` で外部プロバイダーへ遷移し、コールバックでサーバーがトークンを保存。
- トークンは `OutlookCalendarConnection` / `GoogleCalendarConnection` に自動保存され、ユーザーは値を入力しない。エラー時は管理画面へステータスメッセージを返してリダイレクト。
- 同期そのものは `ExternalCalendarSyncService` を経由（イベント同期 UI は今後の実装に委ねる）。
### 8.4 `ICloudSettingController`
- ユーザーにつき 1 件の認証情報を `Index`/`Create`/`Edit`/`Delete` で管理。重複登録は `ModelState` エラーに。
### 8.5 Identity 管理系
- `UsersController`: ユーザーの作成・編集・削除。メール重複チェックとパスワード入力必須。
- `RolesController`: `IdentityRole` の CRUD。
- `UserRolesController`: `IdentityUserRole<string>` の追加・更新・削除 (`UserManager.AddToRoleAsync` / `RemoveFromRoleAsync`)。
### 8.6 `HomeController`
- `Index`, `Privacy`, `Error` を提供。認証後のデフォルト遷移先。
- `Privacy` は `AppNotice` テーブルからアップデート/障害情報を取得して表示する（未登録の場合は空メッセージ）。
### 8.7 `AppNoticesController`
- Admin ロール向けにプライバシーポリシーの告知（アップデート/障害）を CRUD 管理する。
- `/AppNotices/Index` で一覧、`Create`/`Edit`/`Delete` を提供。バージョンは任意、箇条書きは改行区切りで保存。

## 9. UI / UX 指針
- **レイアウト (`Views/Shared/_Layout.cshtml` + `wwwroot/css/site.css`):**
  - 画面全体にグラデーション背景を敷き、ガラス調ナビゲーション (ブランドピル + 丸型メニュー) を配置。
  - `.app-hero` と `.app-content` でカードが背景から浮き上がる構成。
- **カレンダー (`Views/Events/Index.cshtml` + `wwwroot/css/calendar-ui.css` + `wwwroot/css/events-integrated.css` + `wwwroot/js/events-integrated.js`):**
  - 左サイドでソース/カテゴリのフィルター（カテゴリは DB マスタから動的生成）、右サイドで検索・統計・直近予定を配置した統合カレンダー UI。中央に FullCalendar を配置し、月/週/日ビュー切替と [今日][同期][新規追加] ボタンを備える。
  - FullCalendar に拡張メタを渡し、フィルター/統計用に利用する。同期ボタンは `/Events/Sync` (iCloud) を AJAX 呼び出し、Outlook/Google 同期ボタンは `ExternalCalendars/Sync` へ POST。
  - モバイル/タブレット（～1024px）は1カラム化し、クイックアクション＋シート表示を採用。リストビューは時間/タイトルを1行扱いのレイアウトで重なりを防止。
- **フォーム/詳細 (`Views/Events/Create|Edit|Details|Delete` + `wwwroot/css/event-forms.css` + `wwwroot/js/event-forms.js`):**
  - 円弧の大きいカード、丸みのある入力欄、グラデーションボタンを採用。
  - プレビューカードとカラースウォッチで UI の雰囲気を即時確認可能 (`event-forms.js` がタイトル/開始時刻/色をリアルタイム反映)。
  - ソース/カテゴリ/優先度/場所/参加者/繰り返し/リマインダーの入力欄を追加済みで、繰り返しはカレンダー表示・直近予定に自動展開し、リマインダーとともにバッジ表示する（発火通知やバックエンド側の実計算は未実装）。単発スキップは `RecurrenceExceptions` で保持し、UI展開時に除外する。
  - 削除画面では繰り返しイベントの場合に「この発生のみ / この日以降を削除 / すべて削除」を選択可能。場所が URL の場合はリンクとして表示し、新しいタブで開く。
  - 詳細/削除画面はモーダル風シート (`details-sheet`, `details-grid`) で表示し、閉じる/編集/削除ボタンを横並びに配置。
- **アカウント設定 (`Areas/Identity/Pages/Account/Manage/*` + `wwwroot/css/account-manage.css`):**
  - `_Layout.cshtml` でガラス調ヘッダー＋サイドナビを備えたカード UI に統一し、ナビ文言や各ページ内文書を日本語化。
  - `_ManageNav.cshtml` はカテゴリとラベルを併記したタブ型リンク、`_StatusMessage.cshtml` はトースト表示。
  - プロフィール/メール/パスワード/2FA/個人データ/ICカード/iCloud/Outlook/Google など主要フォームは `settings-card`, `form-grid`, `btn-primary-modern` を用いた操作性重視の配置へ変更。Outlook/Google は「状態表示＋連携/解除ボタン」だけを表示し、トークン入力欄を廃止（必要なら別の管理者専用デバッグ画面を導入）。

## 10. 遷移 (テキスト)
### 10.1 認証済みユーザー
1. `GET /` → `Home/Index`。未ログインなら `/Identity/Account/Login` へリダイレクト。
2. ナビゲーションから `/Events/Index` を選択。
3. 操作: `Create` → `/Events/Create` → `POST` 完了後 `/Events/Index`。イベントクリック → `/Events/Details` → 編集/削除。`Sync` → `POST /Events/Sync` (AJAX) → 成功時に FullCalendar を再描画。
### 10.2 iCloud 認証情報
- `/ICloudSetting/Index` で状況確認。未登録は `/Create`、既存は `/Edit` or `/Delete`。カレンダー同期ボタンは認証情報がある場合のみ実質利用可能。
### 10.3 管理者
- `/Users/Index` → `Create/Edit/Delete`。`/Roles/Index` でロール管理。`/UserRoles/Index` でユーザーとロールの組み合わせを調整。

## 11. バリデーションとエラーハンドリング
- Null 許容警告（例: `EventsController` の一部）が残存。順次 `required` 化または null 許容化する。
- `/Events/Sync` 失敗時はログ出力後 HTTP 500 + `{ "message": "同期に失敗しました。" }` を返却。60 秒以内なら HTTP 429 を返す。
- Identity で `User` が取得できない場合は `Unauthorized` を返し、ログへ警告を残す。

## 12. 保守運用ガイド
- コントローラー/サービス/モデル/フローを変更したら、本書も同じコミットで更新する。
  - 新しいエンドポイントやルートは §8 に追記。
  - 画面遷移が変わったら §10 のテキスト図も更新。
  - モデル構造やマイグレーションを変更したら §6 の表を更新。
  - 新しい外部サービスや設定値を追加したら §2/§4/§7 を更新。
- 図表や詳細設計を追加する場合は `docs/` 配下にテキストまたは画像として配置し、ここから参照する。

## 13. 現行プログラム構成（公開前の想定）
- 基点は Identity の `AspNetUsers`。`Events` は 1 対多、`ICloudSettings` と `ICCards` は 1 対 1 想定（ICCards は Unique 未設定のため要検討）。
- モデルは `Event` / `ICloudSetting` / `ICCard` を ApplicationDbContext に登録済み。`BalanceSheetEntry` や `Personal` は削除済み。
- 予定同期 UI は `EventsController` + `Views/Events/Index.cshtml` + `wwwroot/js/events-integrated.js`（FullCalendar 使用）。ソース/カテゴリ/検索/統計/直近予定/現在時刻インジケーターを備える。IC カードは現状 UI 未連携。
- CalDAV 取り込みは iCloud の新規 UID 挿入のみ（更新/削除の取り込みは未対応）。アプリ側で `Source=ICloud` のイベントを作成・更新・削除した場合は CalDAV へ PUT/DELETE で書き戻す。
- 外部カレンダー連携は Outlook/Google の OAuth 認可コードフローを追加。トークンはユーザー入力不要で `*_CalendarConnection` テーブル（UserId ユニークで 1:1）にサーバー保存し、リフレッシュ処理をサービス層に分離。Encrypted 列だが現状は平文保存のため暗号化 TODO。旧 `ExternalCalendarAccount` はレガシーとして残置。
- セキュリティ想定: `ICloudSettings` の Password は平文列のため KeyVault などへの移行が必須。`ICCards` を 1 対 1 にするなら `UserId` へ Unique 制約を付ける。Outlook/Google のトークンは公開前に暗号化実装と鍵管理を整備し、管理系 `[Authorize]` を有効化。
- 運用: スキーマ変更時は `docs/db-3nf.txt` と本仕様書を同じコミットで更新する。公開前は現行 RDB を軸に内容を維持・更新する。
