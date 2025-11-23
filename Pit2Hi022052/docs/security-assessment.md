# セキュリティ簡易評価（現状メモ）

対象リポジトリ: Pit2Hi022052  
評価日時: 2025-XX-XX  
ステータス: 簡易レビュー（コード観察ベース / 実環境検証なし）

## 所見とリスク

- **OAuthトークン平文保存**  
  `OutlookCalendarService` / `GoogleCalendarService` でアクセストークン・リフレッシュトークンをそのままDBに保存。`TODO: encrypt tokens...` とあるが未実装。侵害時に外部カレンダーへ不正アクセスされる恐れ。  
  対応: ASP.NET Core Data Protection + KeyVault/DPAPI などで暗号化した上で保存する。最小権限スコープを再確認。

- **例外メッセージの露出可能性**  
  `ExternalCalendarSyncService.SyncAsync` で接続がない場合に `InvalidOperationException` を投げる経路が残る（呼び出し側でガードしているが他呼び出し時に500となる恐れ）。  
  対応: 例外ではなく適切な Result/戻り値でハンドリングし、ユーザー向けには汎用メッセージのみ返す。

- **入力値のバリデーション/エラーハンドリング不足**  
  カレンダー連携や同期の範囲 (`from`/`to`) の制約が緩い。極端な範囲指定で時間とコストがかかる可能性。  
  対応: 期間に上限を設ける、サーバー側でもフォールバック範囲に丸める。

- **SQLインジェクションなどのDB攻撃リスク**  
  現状は EF Core の LINQ/パラメータ化クエリのみで Raw SQL が見当たらず、直接的なインジェクションリスクは低い。  
  対応: 今後 Raw SQL/FromSql 使用時は必ずパラメータバインドを徹底し、ユーザー入力はモデルバリデーションで制約する。大きなデータセットを返すクエリにはページング・期間上限を設けてリソースDoSを防ぐ。

- **iCloudメール/CalDAV用アクセスキーの保護**  
  `ICloudSetting` に Apple ID メールとアプリパスワードを平文で保存している。漏洩時に iCloud メール・カレンダーへ不正アクセスされる重大リスク。  
  対応: 平文保存を禁止し、Data Protection などで暗号化したうえで保存するか、より望ましくは外部秘密ストア（KeyVault等）に置きDBには参照キーのみを持たせる。長期保存を避け、不要になったキーは必ず無効化/削除する。

- **CSRF/認可チェック**  
  手動同期 (`EventsController.Sync`) と外部連携同期 (`ExternalCalendarsController.Sync`) は `ValidateAntiForgeryToken` を使用しており、現状OK。外部連携コールバック系は `[Authorize]` でガード済み。  
  対応: 新規エンドポイント追加時も Anti-forgery を必須にすること。

- **コントローラーの認可・アクセス制御**  
  `FlowController` / `ToolsController` は `Admin` ロールでガード済みだが、`EventsController` や `CategoriesController` など他コントローラーは `[Authorize]` が明示されていない（認証必須であるか要再確認）。  
  対応: 認証必須のコントローラー/アクションには `[Authorize]` を付与し、公開が妥当なものだけ `[AllowAnonymous]` とする。ロール制御が必要な機能はロール条件を明示する。

- **情報開示リスク**  
  デフォルトで詳細な警告/例外を返さない設定を維持すること。`app.Environment.IsDevelopment()` 以外では詳細を出さないよう現行設定を維持。

- **データ保護/バックアップ**  
  ローカル開発用設定が本番に混入しない運用が必要（appsettings.Development.json の管理）。バックアップ時にトークン等センシティブ情報の取り扱いに注意。

## 推奨アクション

1. トークン暗号化を最優先で実装（Data Protection + プロセス外キー管理）。  
2. 同期系の入力バリデーション強化（期間のサーバー側制限、例外の握りつぶし禁止）。  
3. 例外ハンドリングを統一し、内部エラーはログのみ・ユーザーへは汎用メッセージ。  
4. セキュリティヘッダー (CSP, X-Content-Type-Options, Referrer-Policy など) をミドルウェアで付与検討。  
5. 定期的な脆弱性スキャンと依存パッケージ更新の運用を定義。

## 補足

- αtestModel 配下は静的ページで API と接続しないためリスク低。  
- フロントのみの Flow/Tools ページは admin ロール制御済み。公開設定が変わる場合は改めて認可設定をレビューすること。
