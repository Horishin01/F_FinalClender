# セキュリティ評価（現行構成ベース）
対象リポジトリ: TimeLedger  
評価日時: 2026-XX-XX  
ステータス: ドキュメント/コード観察ベース（実環境検証なし）

## 主要リスク4大要素（優先度つき）
1. **P0: 外部カレンダーのOAuthトークンを平文保存**  
   - `OutlookCalendarConnection` / `GoogleCalendarConnection` にアクセストークン・リフレッシュトークンを暗号化なしで保存。侵害時に外部カレンダーへ即時不正アクセスされる。
2. **P0: iCloudのApple ID + アプリパスワードを平文保存**  
   - `ICloudSetting` にメールとアプリパスワードをそのまま保存。CalDAVのPUT/DELETE実装が残るため改ざん・削除被害が大きい。
3. **P0: 認可漏れ（[Authorize]未付与）**  
   - `EventsController` / `CategoriesController` など利用頻度の高いコントローラーに `[Authorize]` が付いていない。匿名アクセスが許容されていない機能なら重大な認可欠落となる。
4. **P1: 同期系の入力制約・エラーハンドリング不足**  
   - 同期範囲 (`from`/`to`) の上限が緩くDoS/負荷悪化リスク。`ExternalCalendarSyncService.SyncAsync` などで例外をそのまま投げる経路が残り、500や情報露出の恐れ。

## 詳細所見

- **トークン/秘密情報の保護（P0）**  
  - OAuthトークンがDBに平文保存 (`TODO: encrypt tokens...` が未実装)。  
  - iCloudのApple ID + アプリパスワードも平文保存。CalDAVのPUT/DELETEコードが残置しており、漏洩時に予定改ざんリスクが高い。  
  - 対応: ASP.NET Core Data Protection + 外部キー管理（KeyVault/DPAPI）で暗号化。理想は秘密情報を外部ストアに置き、DBには参照キーのみ。長期保存を避け、使い終わったトークン/パスワードは必ず無効化・削除。

- **認可/アクセス制御（P0）**  
  - `ExternalCalendarsController` など一部は `[Authorize]` 済みだが、`EventsController` / `CategoriesController` は無保護。`ICloudSettingController` は認可属性がコメントアウト。  
  - 対応: 認証必須とする全コントローラー/アクションに `[Authorize]` を付与し、必要なものだけ `[AllowAnonymous]`。管理系はロール条件を明示する。

- **入力バリデーションと例外処理（P1）**  
  - 同期APIの範囲指定に上限がなく、極端な期間指定で高負荷になる恐れ。  
  - `ExternalCalendarSyncService.SyncAsync` で接続未登録時に `InvalidOperationException` を投げる経路など、500/スタックトレース露出のリスク。  
  - 対応: 期間上限とサーバー側丸めを必須化。例外はドメインエラーとしてResult型で返し、ユーザーには汎用メッセージのみ。ログは内部向けに詳細を残す。

- **CSRF/XSS 等（現状OKだが維持要）**  
  - 同期系エンドポイントは `ValidateAntiForgeryToken` 済み。新規エンドポイントでも同水準を維持する。  
  - Razorは既定でエスケープされるが、外部データをHTMLとして差し込む場合は `Html.Raw` を避け、サニタイズを徹底。

- **セキュリティヘッダー/環境分離（P2）**  
  - 追加ヘッダー (CSP, Referrer-Policy, X-Content-Type-Options, X-Frame-Options) をミドルウェアで付与検討。  
  - `appsettings.Development.json` が本番に混入しない運用を明確化。Data Protectionキーを冗長化し、キーの保管場所をサーバーローカル以外に逃がすことを検討。

## 推奨アクション（優先度順）
1) **P0** トークン/パスワードの暗号化・外部ストア移行を実装。既存データを再暗号化 or 再取得する移行手順を用意。  
2) **P0** `EventsController` / `CategoriesController` / `ICloudSettingController` に `[Authorize]`（必要ならロール条件）を明示し、匿名公開が妥当な箇所だけ `[AllowAnonymous]` に限定。  
3) **P1** 同期APIの入力制限（期間上限、ページング）と例外ハンドリング統一。ユーザーには汎用エラー、詳細はログへ。  
4) **P2** セキュリティヘッダー追加と運用ルール（環境分離、依存パッケージ更新・脆弱性スキャンの定期実施）を整備。  
5) **P2** iCloud書き戻しコードが無効化されていることをドキュメント化し、再有効化時の権限/検証手順を用意する。
