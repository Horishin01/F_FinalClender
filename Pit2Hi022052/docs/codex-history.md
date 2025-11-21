# Codex 作業履歴ログ（テキスト専用）
このファイルは Codex とユーザーのやり取りによる更新履歴や意図を記録します。目的は履歴参照とロールバック判断の短縮です。更新時は日付順で追記してください。

## 運用ルール
- 最終行へ追記する（上書きしない）。
- 1エントリは短く、「日時 / 変更概要 / 意図・備考」をセットで記載。
- コードや設定を変更した場合は、関連ファイルパスを明記。
- ドキュメント更新のたびにここも更新する必要はないが、大きな改変や方針決定は記録する。

## 履歴
- 2025-11-13 Codex: `Personal` モデルを削除。`docs/db-3nf.txt` を更新し、ER図をユーザー基点に整理。セキュリティ前提メモとセキュリティ用ER図を追加。`docs/specification.md` に「現行プログラム構成（公開前の想定）」を追記。目的: 不要モデルの除去と公開前の設計メモ整備。
- 2025-11-14 Codex: 統合カレンダー機能の骨組みを追加。`Event` にソース/カテゴリ/優先度/繰り返し等を拡張しマイグレーション `20251114113000_AddIntegratedCalendarFields` を手動作成。API (`CalendarApiController`)、ビュー (`Views/Calendar/Integrated`)、JS/CSS を追加し、仕様書 `docs/IntegratedCalendar_仕様.md` を作成。同期は既存 iCloud のみ呼び出しで、Google/Outlook/Work は将来拡張前提。
- 2025-11-14 Codex: 要望により統合カレンダー用の新規コントローラー/ビュー/JS/CSS/仕様書を削除（`CalendarController`、`CalendarApiController`、`Views/Calendar/Integrated.cshtml`、`wwwroot/js|css/calendar-integrated.*`、`docs/IntegratedCalendar_仕様.md`）。Eventモデル拡張とマイグレーションは残存。
- 2025-11-17 Codex: 仕様書を現行コードに合わせて整理（Event拡張カラムの記載、CalDAVが新規UIDのみ挿入である点、統合カレンダーUI/フィルター/統計、繰り返し・通知が保存のみで処理未実装である旨を明記）。`docs/db-3nf.txt` で Events テーブルの新カラムを反映。
- 2025-11-18 Codex: カテゴリをマスタ化（`CalendarCategory`追加、Eventsは`CategoryId` FKへ変更）。カテゴリCRUD (`CategoriesController` + Views)と動的フィルタ/UI反映を実装。`CalendarCategory` に `UserId` を追加しユーザー単位で管理。`docs/db-3nf.txt` と `docs/specification.md` をカテゴリ設計・UI変更に合わせて更新。
- 2025-11-20 Codex: Outlook/Google 連携の土台を追加（`ExternalCalendarAccount` モデル/テーブル、`ExternalCalendarsController` とビュー、`ExternalCalendarSyncService` スタブ、Events画面に同期ボタン）。ドキュメントに外部カレンダー連携を追記。
- 2025-11-20 Codex: 外部カレンダー設定をアカウント設定配下で Outlook/Google 別ページに分離。ドキュメントは UI/コントローラ構成変更に合わせて更新。
- 2026-03-13 Codex: Outlook/Google 連携を本番向け OAuth 認可コードフローに刷新（`OutlookCalendarConnection`/`GoogleCalendarConnection` 追加、`AuthController` で connect/callback、Manage ページを状態表示+連携/解除ボタン化、トークン手入力廃止）。`docs/specification.md` と `docs/db-3nf.txt` を更新。
