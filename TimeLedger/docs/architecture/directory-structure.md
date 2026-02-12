# ディレクトリ構成（作成日: 2026-02-12）

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
|-- αtestModel/                      # 単体UIモック(index.html, style.css)
|-- appsettings*.json                # 環境別設定
|-- Program.cs                       # ASP.NET Coreエントリーポイント
|-- TimeLedger.csproj / TimeLedger.sln
|-- NuGet.Config
|-- bin/                             # ビルド成果物
`-- obj/                             # 中間生成物
```

## メモ
- `docs/` は用途別サブフォルダに分類済み（architecture/operations/security/requirements/testing/history/prototypes）。新規追加時は適切なフォルダに配置してください。
- `bin/` と `obj/` はビルド成果物なので、容量が必要になった場合はクリーン可能です。
- `CodeX_date/` と `αtestModel/` はアプリ本体とは独立したHTMLプロトタイプ置き場です。
