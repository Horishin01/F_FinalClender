# スタイルガイド（更新日: 2026-02-12）

## C# / ASP.NET Core
- C# 12 / .NET 8 を前提に Nullable を有効化。public API では null 許容を明示。
- 依存注入はコンストラクターインジェクションを基本とし、サービスはインターフェース経由で解決する。
- 非同期メソッドは `Async` サフィックスを付け、`ConfigureAwait(false)` はライブラリコードでのみ使用。
- コントローラーは薄く保ち、ビジネスロジックはサービス層に置く。入力検証は `ModelState` と DataAnnotations を併用。
- ロギングは `ILogger<T>` を使用し、PII を含めない。例外はキャッチしてドメインエラーに変換し、UI には汎用メッセージを返す。

## Razor / MVC
- TagHelper を積極利用し、URL/フォームは `asp-action`/`asp-controller` で記述する。
- 全 POST は `[ValidateAntiForgeryToken]` を付与し、AJAX もヘッダー `RequestVerificationToken` を送る。
- 共通 UI は `Views/Shared` のパーシャル/レイアウトに集約し、重複したマークアップを避ける。
- 認可による表示制御（ロール別ボタン非表示など）を Razor 側でも行う。

## JavaScript
- 既存方針に合わせて Vanilla JS を使用。グローバル汚染を避け、即時関数またはモジュールパターンでまとめる。
- DOM 取得・イベント登録は `data-*` 属性ベースで行い、CSS クラスをロジックのキーに使わない。
- FullCalendar 連携コード (`wwwroot/js/events-integrated.js`) に機能を集中させ、API レスポンスのスキーマ変更時はここを更新。

## CSS / デザイン
- テーマはグラデーション背景 + ガラス調カードを基調にし、一貫したスペーシング（例: 4/8/12/16px スケール）を使う。
- 新規コンポーネントは既存の `*-card`, `btn-primary-modern` などのトークンを再利用し、色コードを直書きしない（CSS 変数を活用）。
- レスポンシブ対応を必須とし、768px/1024px を主要ブレークポイントにする。

## DB / マイグレーション
- モデル変更時は必ずマイグレーションを追加し、`docs/data-model.md` に反映する。
- 破壊的変更はリリースノートやデプロイ手順に追記し、バックアップ/ロールバック手順を明示する。

## コードオーガナイズ
- 拡張メソッドは `Extensions/`、共通ロジックは `Services/` に集約。UI 専用の DTO は `ViewModels/` に配置。
- 名前付け: クラスは PascalCase、メソッドは PascalCase、非公開フィールドは `_camelCase`、定数は `PascalCase`。
