# プロトタイプ置き場メモ（更新日: 2026-02-12）

## 目的
本番コードと切り離した UI/UX 検証用の静的プロトタイプを整理する。

## 現在のファイル
- `CodeX_date/enhanced-calendar-app.html` : カレンダー UI の強化案。
- `CodeX_date/flow-page (1).html` : フロー/タスク系画面の試作。
- `αtestModel/index.html`, `αtestModel/style.css` : 別デザインのモック。

## 使い方
- いずれも静的 HTML のため、ブラウザで直接開くだけで確認できる。サーバー側の依存やビルド手順は不要。

## 運用ルール
- 本番ビルドに混入させない（`dotnet publish` の出力に含めない）。必要なら `docs/prototypes` 配下へ移動して一元管理する。
- プロトタイプで決定した UI/挙動は、該当する Razor/JS/CSS へ反映したうえで、この README に反映状況をメモする。
