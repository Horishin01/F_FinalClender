# ディレクトリ構成（更新日: 2026-08-25）

リポジトリの全体像を素早く把握するためのマップです。パスはリポジトリルート基準。

## 概要
```
TimeLedger/
|-- Areas/
|   `-- Identity/Pages/              # ASP.NET Core Identity UI
|-- CodeX_date/                      # カレンダー/フロー画面のHTML試作
|-- Controllers/                     # MVCコントローラー
|-- Data/                            # DbContextと初期データ
|-- Extensions/                      # 拡張メソッド・ヘルパー
|-- Middleware/                      # カスタムミドルウェア
|-- Migrations/                      # EF Coreマイグレーション
|-- Models/                          # ドメインモデル
|-- Properties/                      # アセンブリ情報・起動設定
|-- Services/                        # アプリサービスとインターフェース
|-- ViewModels/                      # ビューモデルDTO
|-- Views/
|   |-- Admin/                       # 管理画面
|   |-- Analytics/                   # 分析・レポート
|   |-- AppNotices/                  # お知らせ表示
|   |-- Calendar/                    # カレンダー画面
|   |-- Categories/                  # カテゴリ管理
|   |-- Events/                      # イベント関連ビュー
|   |-- ExternalCalendars/           # 外部カレンダー連携UI
|   |-- Flow/                        # フロー/タスク画面
|   |-- Home/                        # ホーム/ダッシュボード
|   |-- Roles/                       # ロール管理
|   |-- Shared/                      # 共有レイアウト・部分ビュー
|   |-- Tools/                       # ツール系ページ
|   |-- UserRoles/                   # ユーザーとロールの紐付け
|   `-- Users/                       # ユーザー管理
|-- wwwroot/
|   |-- css/                         # ビルド済みCSS
|   |-- icons/                       # 静的アイコン
|   |-- js/                          # クライアントスクリプト
|   `-- lib/                         # サードパーティ(lib/bootstrap, fullcalendar, jquery, fontawesome)
|-- docs/                            # プロジェクトドキュメント
|   |-- architecture/                # アーキ概要・データモデル・スタイルガイド
|   |-- requirements/                # 仕様/要件/技術スタック/データ設計
|   |-- operations/                  # セットアップ・デプロイ・運用Runbook
|   |-- security/                    # セキュリティ方針と評価
|   |-- testing/                     # テスト方針/チェックリスト
|   |-- history/                     # 変更履歴メモ
|   `-- prototypes/                  # 静的UIモック置き場
|-- database/                        # PostgreSQL本体・接続設定・運用
|   |-- config/                      # Git管理外envと追跡対象example
|   |-- scripts/                     # 起動・バックアップ・移行スクリプト
|   |-- runtime/                     # DB物理データ/ダンプ（全体をGit除外）
|   |-- compose.development.yaml     # 開発DB（127.0.0.1:55432）
|   `-- compose.production.yaml      # 本番DB（127.0.0.1:5432）
|-- deploy/                          # 本番配備ひな型
|   |-- nginx/                       # TLS終端・リバースプロキシ
|   `-- systemd/                     # アプリサービス
|-- 動作確認/                        # 構成回帰確認PowerShell
|-- αtestModel/                      # 単体UIモック(index.html, style.css)
|-- appsettings*.json                # 共有設定とGit管理外Development Local設定
|-- Program.cs                       # ASP.NET Coreエントリーポイント
|-- TimeLedger.csproj / TimeLedger.sln
|-- NuGet.Config
|-- bin/                             # ビルド成果物
`-- obj/                             # 中間生成物
```

## メモ
- `docs/` は用途別サブフォルダに分類済み（architecture/operations/security/requirements/testing/history/prototypes）。新規追加時は適切なフォルダに配置してください。
- DB関連は `database/`、Web/サービス配備例は `deploy/`、回帰確認スクリプトは `動作確認/` に置く。
- `database/config/*.env`、`database/runtime/`、`appsettings.Development.Local.json` は秘密値または実データを含むためGit管理外。`database/config/*.env.example` だけを共有する。
- `bin/` と `obj/` はビルド成果物なので、容量が必要になった場合はクリーン可能です。
- `CodeX_date/` と `αtestModel/` はアプリ本体とは独立したHTMLプロトタイプ置き場です。
