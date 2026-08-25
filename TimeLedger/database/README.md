# TimeLedger PostgreSQL

PostgreSQLの接続設定、DB本体、バックアップをプロジェクト直下の `database/` に集約する。開発と本番はコンテナ・ポート・保存先を分離し、実データと秘密値はGitへ登録しない。

## 構成

```text
database/
|-- compose.development.yaml       # 開発DB（127.0.0.1:55432）
|-- compose.production.yaml        # 本番DB（127.0.0.1:5432）
|-- config/
|   |-- development.env.example    # 開発用ひな型
|   `-- production.env.example     # 本番DB・アプリ接続設定ひな型
|-- scripts/
|   |-- Database.ps1               # 起動・停止・状態・バックアップ・復元
|   |-- database.sh                # Ubuntu用の同等運用スクリプト
|   `-- Migrate-DevelopmentDatabase.ps1
|                                     # 既存開発DBを配下へ複製して接続切替
`-- runtime/                        # 初回起動時に作成（全体をGit除外）
    |-- development/{data,backups}
    `-- production/{data,backups}
```

`data/` はPostgreSQLの物理データ、`backups/` は `pg_dump -Fc` の論理バックアップである。PostgreSQLのメジャーバージョン変更時は物理データを直接コピーせず、対応する `pg_dump` / `pg_restore` または `pg_upgrade` で移行する。

## 前提

- Windows開発: Docker Desktop（Linuxコンテナ）とDocker Compose v2
- Ubuntu本番: Docker EngineとDocker Compose plugin
- 使用イメージ: `postgres:16.15`

コンテナのポートはloopbackだけに公開する。開発DBは `55432`、本番DBは `5432` であり、外部ネットワークへPostgreSQLを直接公開しない。

## 開発DBを既存接続先から移す

1. `appsettings.Development.Local.json` が現在の移行元DBを参照していることを確認する。
2. Docker Desktopを起動する。
3. プロジェクト直下のPowerShellで次を実行する。

```powershell
./database/scripts/Migrate-DevelopmentDatabase.ps1
```

Docker導入前にGit管理外の接続設定と保存先だけ準備する場合は `-PrepareOnly` を付ける。この場合、F5の接続先は変更しない。

スクリプトは次の順で処理する。

1. ランダムなローカルDBパスワードを生成し、Git管理外の `database/config/development.env` を作成する。
2. DB本体を `database/runtime/development/data` に持つPostgreSQLを起動する。
3. 現在のDBを `database/runtime/development/backups` へ論理バックアップする。
4. ローカルDBへ復元して `select 1` を確認する。
5. 成功した場合だけ `appsettings.Development.Local.json` の接続先を `127.0.0.1:55432` に切り替える。旧Local設定も同じバックアップ領域へ保存する。

途中で失敗した場合はF5の接続先を変更しない。移行元DBは読み取りだけで、削除・停止・更新しない。

移行後の日常操作:

```powershell
./database/scripts/Database.ps1 -Environment Development -Action Start
./database/scripts/Database.ps1 -Environment Development -Action Status
./database/scripts/Database.ps1 -Environment Development -Action Backup
./database/scripts/Database.ps1 -Environment Development -Action Stop
```

## 新規の開発DBを作る

既存DBを引き継がない場合は `development.env.example` を `development.env` へコピーし、例示パスワードを変更する。`appsettings.Development.Local.json` の `DefaultConnection` も同じDB名・ユーザー・パスワードと `Port=55432` に合わせ、DB起動後にマイグレーションを適用する。

```powershell
Copy-Item ./database/config/development.env.example ./database/config/development.env
./database/scripts/Database.ps1 -Environment Development -Action Start
dotnet ef database update --project ./TimeLedger.csproj --startup-project ./TimeLedger.csproj
```

## 本番DBを配下へ移す

本番移行は停止時間、移行元、DBバージョン、容量を確認してから行う。開発用の自動移行スクリプトを本番へ流用しない。

1. `production.env.example` をGit管理外の `production.env` へコピーし、DB名・ユーザー・十分に長いランダムパスワード・実 `AllowedHosts` を設定する。
2. `chmod 600 database/config/production.env` とし、所有者をTimeLedger運用ユーザーに限定する。
3. 移行元へ書き込むアプリを停止し、移行元と同じか新しいメジャーバージョンの `pg_dump -Fc` で最終バックアップを取得する。
4. バックアップを `database/runtime/production/backups/` に置く。
5. 本番コンテナを起動し、復元前バックアップを取れる状態で `pg_restore --clean --if-exists --no-owner --no-privileges` を実行する。
6. `select 1`、EFマイグレーション履歴、主要テーブル件数、ログインを確認してからアプリを再開する。
7. 切替後も移行元と最終バックアップを保持し、ロールバック可能にする。

起動例:

```bash
mkdir -p database/runtime/production/data database/runtime/production/backups
docker compose \
  --env-file database/config/production.env \
  -f database/compose.production.yaml \
  up -d --wait
```

Ubuntuでの日常操作:

```bash
./database/scripts/database.sh production start
./database/scripts/database.sh production status
./database/scripts/database.sh production backup
./database/scripts/database.sh production stop
```

アプリのsystemdユニットは `deploy/systemd/timeledger.service.example` の `EnvironmentFile` から同じ `production.env` を読み、`ConnectionStrings__DefaultConnection` を使用する。

## バックアップと復元

```powershell
./database/scripts/Database.ps1 -Environment Production -Action Backup
./database/scripts/Database.ps1 -Environment Production -Action Restore `
  -BackupFile ./database/runtime/production/backups/<file>.dump -Force
```

Ubuntuで復元する場合は `./database/scripts/database.sh production restore <dump> --force` を使用する。

復元は破壊的なため `-Force` が必須で、実行直前にも自動バックアップを作る。アプリからのDB書き込みを停止し、復元後に必ず整合性を確認する。

## データ保護

- `database/config/*.env`、`database/runtime/`、`appsettings.Development.Local.json` はGit管理外である。
- `git clean -x`、プロジェクトフォルダの再作成、無確認のDockerボリューム削除を実行しない。
- プロジェクト配下に置くこと自体はバックアップではない。`backups/` の最新ダンプを別ディスクまたは承認済みバックアップ先へ暗号化して複製する。
- 本番DBの実データや接続情報を開発端末・Issue・チャット・ログへ貼り付けない。
