# データモデル概要（更新日: 2026-02-12）

## 主なエンティティ
- **ApplicationUser**: IdentityUser を拡張。ほぼ標準スキーマ（Email 確認必須）。各ドメインテーブルから `UserId` FK で参照。
- **Event** (`Models/Event.cs`): 予定本体。`UserId`(FK→AspNetUsers)、`CategoryId`(FK→CalendarCategory)、`Source`(Local/Google/ICloud/Outlook/Work)、優先度/場所/参加者/繰り返し/例外日/リマインダーを保持。FullCalendar 用に `AllDay` と `IsAllDay` を併存。UID は外部同期時に使用。
- **CalendarCategory**: ユーザー単位のカテゴリマスタ。`Name`/`Icon`/`Color` を持ち、Event から FK 参照。
- **OutlookCalendarConnection / GoogleCalendarConnection**: 各ユーザーにつき1件の OAuth トークンストア。`UserId` にユニークインデックス。現状トークンはプレーン保存のため、暗号化が今後の必須課題。
- **ExternalCalendarAccount**: 旧トークン保存テーブル。移行期間中の互換用途。
- **ICloudSetting**: CalDAV 用の Apple ID + アプリパスワード。プレーン保存のため、暗号化/外部ストア移行が必須。
- **UserAccessLog**: `UserId` と `AccessedAtUtc` の複合インデックスでアクセス履歴を保持。ミドルウェア経由で記録。
- **AppNotice**: アップデート/障害通知。`Kind` と `OccurredAt` にインデックス。
- **ICCard**: ICカード UID とユーザーの紐付け。現状 UI では未使用だが将来拡張を想定。

## リレーション（テキスト）
- ApplicationUser 1 : N Event  
- ApplicationUser 1 : N CalendarCategory  
- ApplicationUser 1 : 1 OutlookCalendarConnection / GoogleCalendarConnection / ICloudSetting / ICCard(将来)  
- CalendarCategory 1 : N Event  
- AppNotice, UserAccessLog はユーザーと疎結合（UserId で関連）

## 運用メモ
- すべてのテーブルは PostgreSQL に作成される。スキーマ変更は `Migrations/` を更新し、`dotnet ef database update` で適用。
- トークン/パスワード系カラムは暗号化未対応。公開前に Data Protection + 外部キー管理を導入し、既存レコードの再暗号化手順を別途用意すること。
