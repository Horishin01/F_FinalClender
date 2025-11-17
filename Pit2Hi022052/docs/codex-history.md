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
