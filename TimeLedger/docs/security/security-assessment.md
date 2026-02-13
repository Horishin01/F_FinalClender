# セキュリティ評価（現行実装再評価）
対象リポジトリ: TimeLedger  
評価日: 2026-02-13  
評価方法: コードベース確認（実環境ペネトレーションテスト未実施）

## 機能別評価（各アプリ）
| 機能領域 | 主な実装 | 期待公開範囲 | 現状評価 |
| --- | --- | --- | --- |
| Home / Privacy / Flow | `HomeController`, `FlowController` | 匿名公開可 | `Flow` は `[AllowAnonymous]` で明示。`Home` も匿名アクセス時に限定表示で成立。 |
| カレンダー本体 | `EventsController` | 認証必須（ユーザー単位） | コントローラー全体に `[Authorize]` がなく、所有者チェック漏れがあるため高リスク。 |
| カテゴリ管理 | `CategoriesController` | 認証必須（ユーザー単位） | 一部はユーザーIDで絞り込み済みだが、`Edit` 更新時に所有者再検証が不足。 |
| 外部連携（OAuth/同期） | `AuthController`, `ExternalCalendarsController`, `*CalendarService` | 認証必須（管理者操作中心） | 認可属性は比較的整備。トークン保存が平文で重大課題。 |
| アカウント拡張（iCloud/ICカード/Outlook/Google） | `Areas/Identity/Pages/Account/Manage/*` | 管理者操作 + 一般ユーザー閲覧 | 管理者制御と α フラグは機能。秘密情報平文保存が継続。 |
| 管理運用アプリ | `Admin/Analytics/AppNotices/Tools/Users/Roles/UserRoles` | 管理者限定 | `Admin/Analytics/AppNotices/Tools` はガード済み。`Users/Roles/UserRoles` は未ガードで最重要リスク。 |
| 旧 iCloud 設定 MVC | `ICloudSettingController` | 原則未使用/廃止対象 | 認可属性がコメントアウトされ残置。新しい Razor Pages 実装と二重化。 |

## 優先度付き主要リスク
1. **P0: 管理系エンドポイントの認可漏れ**
- `UsersController`, `RolesController`, `UserRolesController` に `[Authorize]` / ロール制約が未適用。
- 影響: ユーザー/ロール管理が匿名または一般ユーザーから到達可能になる恐れ。

2. **P0: イベント/カテゴリの所有者チェック欠落**
- `EventsController` の `Edit/Details/Delete` 系で ID 指定のみの参照経路が存在。
- `EventsController` の `Delete` は `currentUser == null` 条件を含み、匿名時に不適切な条件成立が起こり得る。
- `CategoriesController` の `Edit` 更新時に既存レコードの所有者照合が不足。

3. **P0: 秘密情報の平文保存**
- `ICloudSetting.Password` が平文保存。
- `OutlookCalendarConnection` / `GoogleCalendarConnection` は列名が `*Encrypted` だが実装上は平文保存（TODO コメントあり）。
- `ExternalCalendarAccount`（legacy）にも平文トークン列が残存。

4. **P1: 旧実装の残置による攻撃面積拡大**
- `ICloudSettingController` が新しい Identity 管理ページ実装と併存し、認可未適用の古い経路を残している。

5. **P1: 初期管理者の固定資格情報**
- `Program.cs` のシードで固定メール/パスワードが埋め込み。
- 運用ミス時に即時侵害リスクへ直結。

6. **P2: セキュリティヘッダー未整備**
- CSP / Referrer-Policy / X-Content-Type-Options / X-Frame-Options が未実装。

## 既存の防御策（維持すべき点）
- 多くの POST に `ValidateAntiForgeryToken` を適用済み。
- 本番パイプラインで `UseExceptionHandler` / `UseHsts` を有効化。
- `AuthController` と `ExternalCalendarsController` は `[Authorize]` が明示されている。
- アカウント拡張ページ（Outlook/Google/iCloud/ICカード）は `AlphaFeatureFlags` と管理者判定で操作制限。
- `Events/Sync` は 60 秒クールダウンで過負荷を軽減。

## 改善ロードマップ（推奨）
1. **即時（リリース前必須）**
- `UsersController`, `RolesController`, `UserRolesController` に `[Authorize(Roles = RoleNames.Admin)]` を付与。
- `EventsController`, `CategoriesController` に `[Authorize]` を付与し、全更新/参照で `UserId` 所有者照合を強制。
- `ICloudSettingController`（旧 MVC）をルーティング対象から除外または削除。

2. **短期（次スプリント）**
- iCloud/OAuth トークンの暗号化保存を実装（Data Protection + 鍵管理）。
- 既存平文データの再取得/再暗号化と強制再認可手順を用意。
- 初期管理者資格情報を環境変数化し、固定値シードを廃止。

3. **中期（運用品質向上）**
- 認可回帰テスト（匿名/一般/管理者）を自動化し、PR の必須チェック化。
- セキュリティヘッダーをミドルウェアで統一付与。
