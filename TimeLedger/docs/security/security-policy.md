# セキュリティポリシー（更新日: 2026-02-13）

## 適用範囲
- 本書は `TimeLedger` の全機能（カレンダー、外部連携、管理機能、Flow/Tools を含む）に適用する。

## 認証・認可ポリシー
- デフォルトは「認証必須」。
- 匿名公開は `Home` / `Privacy` / `Flow` など明示された画面のみ許可する。
- 一般ユーザー機能（イベント、カテゴリ、外部連携参照）は `[Authorize]` を必須とする。
- 管理機能（`Admin` / `Analytics` / `AppNotices` / `Users` / `Roles` / `UserRoles` / `Tools`）は `[Authorize(Roles = "Admin")]` を必須とする。
- 参照・更新・削除はすべてサーバー側で `UserId` 所有者照合を行う。

## 秘密情報ポリシー
- 接続文字列、OAuth クライアントシークレット、トークン、iCloud アプリパスワードは平文保存しない。
- 保存時は暗号化を必須とし、鍵はアプリ外（例: Key Vault / Secret Manager / OS 保護ストア）で管理する。
- ログ・ダンプ・バックアップへ秘密情報を出力しない。

## Web セキュリティポリシー
- POST 系エンドポイントは CSRF 対策（`ValidateAntiForgeryToken`）を必須とする。
- 本番は HTTPS 必須、`UseHsts()` を有効化する。
- セキュリティヘッダーを付与する。
- 対象ヘッダー: `Content-Security-Policy`, `Referrer-Policy`, `X-Content-Type-Options`, `X-Frame-Options`.

## ログ・監査ポリシー
- エラー詳細は内部ログに限定し、画面には汎用メッセージのみ返す。
- 監査ログ（`UserAccessLog`）は保持期間と削除方針を定める。
- 重大インシデント時はトークン失効と強制再認証を優先する。

## デプロイ前セキュリティゲート
- 以下を満たさない場合は本番反映しない。
- 管理系エンドポイントのロール制御が有効。
- 主要更新 API の所有者照合が有効。
- 平文保存される秘密情報がない。
- 既知の高リスク項目がトラッキングされ、回避策が運用に反映済み。

## 現状ギャップ（2026-02-13 時点）
- `UsersController` / `RolesController` / `UserRolesController` の管理者制御が未適用。
- `EventsController` / `CategoriesController` で一部所有者照合が不十分。
- iCloud/OAuth トークンが実質平文で保存されている。
- 旧 `ICloudSettingController` が残置されている。
