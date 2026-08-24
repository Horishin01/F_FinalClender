# TimeLedger データベース運用

開発ではプロジェクト配下のSQLiteファイルを使い、DockerやDBサーバーは使用しない。本番のPostgreSQL接続設定、DB本体、バックアップはプロジェクト直下の `database/` に集約する。実データと秘密値はGitへ登録しない。

## 構成

```text
TimeLedger/
|-- timeledger.db                  # 開発用SQLite DB（Git管理外）
`-- database/
    |-- compose.production.yaml    # 本番DB（127.0.0.1:5432）
    |-- config/
    |   `-- production.env.example # 本番DB・アプリ接続設定ひな型
    |-- scripts/
    |   |-- Database.ps1           # 本番DBの起動・停止・状態・バックアップ・復元
    |   `-- database.sh            # Ubuntu用の同等運用スクリプト
    `-- runtime/production/{data,backups}
```

`../timeledger.db` は開発用SQLiteの単一ファイルである。SQLiteが作る `timeledger.db-wal` と `timeledger.db-shm` も同じ場所に置く。`production/data/` はPostgreSQLの物理データ、`production/backups/` は `pg_dump -Fc` の論理バックアップである。PostgreSQLのメジャーバージョン変更時は物理データを直接コピーせず、対応する `pg_dump` / `pg_restore` または `pg_upgrade` で移行する。

## 前提

- Ubuntu本番: Docker EngineとDocker Compose plugin
- 使用イメージ: `postgres:16.15`

本番コンテナのポートはloopbackの `5432` だけに公開し、外部ネットワークへPostgreSQLを直接公開しない。

## 開発DBを作る

開発DBは `TimeLedger/timeledger.db` に作られるSQLiteファイルで、事前設定、Docker、接続資格情報は不要。

```powershell
dotnet run --project ./TimeLedger.csproj
```

初回起動時にテーブルを自動作成する。モデル変更で作り直す場合は、必要なデータを退避してから `timeledger.db` を削除し、再起動する。旧開発用Docker PostgreSQLはデータ・バックアップが空であることを確認して撤去済みである。

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
