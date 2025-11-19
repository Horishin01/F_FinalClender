# Pit2Hi022052 システム仕様書

_最終更新: 2025-11-17。仕様書とコードコメントは日本語を基本言語とする。挙動・ルート・データモデル・フローを変更した場合は、本書も同じコミットで更新すること。_

## 1. 目的と適用範囲
- iCloud (CalDAV) の予定をユーザー単位で取得し、FullCalendar 上で可視化しながら PostgreSQL に保存・編集できる Web アプリを提供する。
- Visual Studio / VS Code どちらからでも同等の操作ができるよう、ユーザー管理・ロール管理・割り当て画面を備える。
- 各ユーザーの iCloud 認証情報を保持し、認証済み同期ジョブを実行できる仕組みを維持する。

## 2. アーキテクチャ概要
- **プラットフォーム:** ASP.NET Core 8 (MVC + Razor Pages、Identity 領域付き)。
- **言語 / ターゲット:** C# 12、.NET 8.0 (`Pit2Hi022052.csproj`)。
- **主要ライブラリ:** ASP.NET Core Identity、Entity Framework Core (Npgsql)、Ical.Net、PCSC/PCSC.Iso7816、Microsoft.VisualStudio.Web.CodeGeneration.Design など。
- **フロントエンド:** FullCalendar を `wwwroot/js/events-integrated.js` で初期化し、素の JavaScript で UI 操作を実装。
- **ホスティング:** Kestrel。デバッグ時は `https://localhost:7052;http://localhost:5016` を使用。

## 3. 実行時構成 (Program.cs)
1. `DefaultConnection` を構成ファイルから読み込み (未設定時は例外)。
2. `ApplicationDbContext` を Npgsql プロバイダーで登録。
3. Identity を設定 (`ApplicationUser` + Roles、メール確認必須、Razor Pages / MVCを登録)。
4. `AddHttpContextAccessor`、`ICloudCalDavService`、`IcalParserService`、`AddMemoryCache`、`AddAntiforgery`(ヘッダー `RequestVerificationToken`) を DI へ追加。
5. パイプライン: Development=`UseMigrationsEndPoint`、Production=`UseExceptionHandler("/Home/Error")`+`UseHsts()`、共通=HTTPS 強制/静的ファイル/StatusCodePages/Routing/Authentication/Authorization、既定ルート `{controller=Home}/{action=Index}/{id?}/{id2?}` + Razor Pages。

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

## 6. ドメインモデル概要
| エンティティ | 主なフィールド | 目的 / メモ |
| --- | --- | --- |
| `Event` (`Models/Event.cs`) | `Id`, `UserId`, `UID`, `Title`, `StartDate`, `EndDate`, `LastModified`, `Description`, `AllDay` (+ UI 用 `IsAllDay`)、`Source`,`Category`,`Priority`,`Location`,`AttendeesCsv`,`Recurrence`,`ReminderMinutesBefore` | カレンダー予定。`UID` で iCloud イベントと紐付け。開始/終了は null 許容。ソース/カテゴリ/優先度/参加者/繰り返し/リマインダーは現状保存のみ（処理は未実装）。 |
| `ICloudSetting` (`Models/ICloudSetting.cs`) | `Id`(GUID文字列), `UserId`, `Username`, `Password` | iCloud 認証情報。現在は平文保存のため暗号化対応が今後の課題。 |
| `ICCard` (`Models/ICCard.cs`) | `Id`, `UserId`, `Uid` | IC カード UID とユーザーの紐付け用。現状 UI からは未使用。 |
| Identity テーブル | `AspNetUsers`, `AspNetRoles`, `AspNetUserRoles` など | マイグレーション (`Migrations/*`) で生成。 |

## 7. サービスと外部連携
- **CloudCalDavService (`Services/CloudCalDavService.cs`):**
  - `/.well-known/caldav` → `calendar-home-set` → `REPORT` の順で CalDAV から予定を取得し、IcalParserService で ICS を解析後 `Events` に保存。
  - 新規 UID のみ保存（既存レコードの更新/削除は未反映）。処理件数 (scanned/saved) を返却。60 秒に 1 回のレート制限は `IMemoryCache` で実装。
  - `/Events/Sync` 呼び出し時は CSRF 対策ヘッダー `RequestVerificationToken` を必須化。
- **IcalParserService (`Services/IcalParserService.cs`):**
  - Ical.Net を用いて ICS 文字列を `Event` リストへ変換。UID 重複排除、終日判定、最終更新日時を付与。取り込み時の `Source`/`Category`/`Priority`/`Location` などの拡張メタは設定していない（UI ではデフォルト値で表示）。
- **PCSC ライブラリ:** `PCSC` / `PCSC.Iso7816` を参照済み。IC カード機能を実装する際は要件とセキュリティを別途整理する。

## 8. コントローラーと機能
### 8.1 `EventsController`
- `Index`: ログインユーザーの予定一覧。FullCalendar 用 JSON (`GetEvents`) と CalDAV 同期 (`Sync`: 60 秒クールダウン、結果 JSON) を提供。JSON は拡張メタ（ソース/カテゴリ/優先度など）を extendedProps として返す。
- `Sync`: CalDAV から新規 UID だけを追加（更新/削除は未対応）。戻り値は { saved, scanned, durationMs }。
- `Create` / `Edit` / `Delete` / `Details`: GUID ID による CRUD。`Create` は `startDate`/`endDate` をクエリで受け取り初期値をセット。`Edit` は同時更新例外をハンドリング。
### 8.2 `ICloudSettingController`
- ユーザーにつき 1 件の認証情報を `Index`/`Create`/`Edit`/`Delete` で管理。重複登録は `ModelState` エラーに。
### 8.3 Identity 管理系
- `UsersController`: ユーザーの作成・編集・削除。メール重複チェックとパスワード入力必須。
- `RolesController`: `IdentityRole` の CRUD。
- `UserRolesController`: `IdentityUserRole<string>` の追加・更新・削除 (`UserManager.AddToRoleAsync` / `RemoveFromRoleAsync`)。
### 8.4 `HomeController`
- `Index`, `Privacy`, `Error` を提供。認証後のデフォルト遷移先。

## 9. UI / UX 指針
- **レイアウト (`Views/Shared/_Layout.cshtml` + `wwwroot/css/site.css`):**
  - 画面全体にグラデーション背景を敷き、ガラス調ナビゲーション (ブランドピル + 丸型メニュー) を配置。
  - `.app-hero` と `.app-content` でカードが背景から浮き上がる構成。
- **カレンダー (`Views/Events/Index.cshtml` + `wwwroot/css/calendar-ui.css` + `wwwroot/css/events-integrated.css` + `wwwroot/js/events-integrated.js`):**
  - 左サイドでソース/カテゴリのフィルター、右サイドで検索・統計・直近予定を配置した統合カレンダー UI。中央に FullCalendar を配置し、月/週/日ビュー切替と [今日][同期][新規追加] ボタンを備える。
  - FullCalendar に拡張メタを渡し、フィルター/統計用に利用する。同期ボタンは `/Events/Sync` を AJAX 呼び出し。
- **フォーム/詳細 (`Views/Events/Create|Edit|Details|Delete` + `wwwroot/css/event-forms.css` + `wwwroot/js/event-forms.js`):**
  - 円弧の大きいカード、丸みのある入力欄、グラデーションボタンを採用。
  - プレビューカードとカラースウォッチで UI の雰囲気を即時確認可能 (`event-forms.js` がタイトル/開始時刻/色をリアルタイム反映)。
  - ソース/カテゴリ/優先度/場所/参加者/繰り返し/リマインダーの入力欄を追加済みだが、繰り返し展開や通知計算は未実装（保存のみ）。
  - 詳細/削除画面はモーダル風シート (`details-sheet`, `details-grid`) で表示し、閉じる/編集/削除ボタンを横並びに配置。
- **アカウント設定 (`Areas/Identity/Pages/Account/Manage/*` + `wwwroot/css/account-manage.css`):**
  - `_Layout.cshtml` でガラス調ヘッダー＋サイドナビを備えたカード UI に統一し、ナビ文言や各ページ内文書を日本語化。
  - `_ManageNav.cshtml` はカテゴリとラベルを併記したタブ型リンク、`_StatusMessage.cshtml` はトースト表示。
  - プロフィール/メール/パスワード/2FA/個人データ/ICカード/iCloud など主要フォームは `settings-card`, `form-grid`, `btn-primary-modern` を用いた操作性重視の配置へ変更。

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
- CalDAV 同期は iCloud の新規 UID 挿入のみ。UID が既存でも更新/削除は反映されない。取り込み時の拡張メタはデフォルト値のまま。
- セキュリティ想定: `ICloudSettings` は暗号化ストア前提（平文保存しない）。`ICCards` を 1 対 1 にするなら `UserId` へ Unique 制約を付ける。本番前に管理系 `[Authorize]` を有効化。
- 運用: スキーマ変更時は `docs/db-3nf.txt` と本仕様書を同じコミットで更新する。公開前は現行 RDB を軸に内容を維持・更新する。
