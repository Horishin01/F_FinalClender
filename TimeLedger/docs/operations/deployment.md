# デプロイ手順（Ubuntu / 更新日: 2026-08-25）

## 前提
- テスト環境 OS は Ubuntu（22.04 LTS / 24.04 LTS）を想定。
- サーバーに .NET 8.0 ランタイム、Docker Engine、Docker Compose pluginがインストール済み。
- PostgreSQLは `database/compose.production.yaml` の16.15を使用し、DB本体を `database/runtime/production/data` に保持する。
- 本番はNginxでTLS終端し、Kestrelは同一サーバーの `127.0.0.1:5016` にHTTPで限定待受する。
- 証明書のSANと一致する実ドメイン、DNSのA/AAAAレコード、外部から到達可能な80/443番ポートが必要。ドメインやDNSを確認できない段階では証明書を発行しない。
- IC カード機能を使う場合は `pcsc-lite` の共有ライブラリとデーモン (`libpcsclite.so.1`, `pcscd`) をインストールする。

### IC カード機能の追加依存
- Ubuntu:
  - `sudo apt-get update`
  - `sudo apt-get install -y libpcsclite1 pcscd pcsc-tools`
  - `sudo systemctl enable --now pcscd`
  - `sudo systemctl status pcscd --no-pager`
  - `ldconfig -p | grep libpcsclite.so.1`

## 必須設定（Git管理外env）
- `database/config/production.env.example` を `database/config/production.env` へコピーし、`chmod 600` と所有者限定を行う。Composeとsystemdアプリは同じファイルを使用する。
- `ConnectionStrings__DefaultConnection` : PostgreSQL 接続文字列
- `ASPNETCORE_ENVIRONMENT=Production`
- `AllowedHosts=<証明書のSANと一致する実ドメイン>`
- Kestrelの既定本番待受は `appsettings.Production.json` の `http://127.0.0.1:5016`。`ASPNETCORE_URLS` で公開IPやHTTPSへ変更しない。
- `ASPNETCORE_PATHBASE=/timeledger`（サブパス配信時のみ。例: `https://example.com/timeledger`）
- `Authentication__Outlook__ClientId` / `Authentication__Outlook__ClientSecret`（Outlook 連携を使う場合）
- `Authentication__Google__ClientId` / `Authentication__Google__ClientSecret`（Google 連携を使う場合）
- Data Protection キーの永続化先（ファイル共有や KeyVault 等）を環境変数や設定で指定することを推奨。

接続文字列、DBパスワード、OAuth秘密情報、初期管理者情報は追跡対象JSON、systemdユニット本文、Markdownへ書かない。実値はGit管理外の `database/config/production.env` または承認済み秘密情報ストアへ置く。本番では `BootstrapAdmin__Enabled=true` を拒否する。

## 本番DBの初期配置

```bash
cd ~/F_FinalClender/TimeLedger
cp database/config/production.env.example database/config/production.env
chmod 600 database/config/production.env
# production.env の例示パスワード、接続文字列、AllowedHostsを実値へ変更する

mkdir -p database/runtime/production/data database/runtime/production/backups
docker compose \
  --env-file database/config/production.env \
  -f database/compose.production.yaml \
  up -d --wait
```

既存本番DBを移す場合は、書き込み停止、最終 `pg_dump -Fc`、配下DBへの復元、件数・ログイン確認、ロールバック確認が必要。移行元を推測して操作せず、`database/README.md` の本番移行手順に従う。

`deploy/systemd/timeledger.service.example` の2個のパスプレースホルダーを実パスへ置換して `/etc/systemd/system/timeledger.service` に配置する。ユニットは `database/config/production.env` をEnvironmentFileとして読み込む。

## HTTPテストと本番HTTPSの境界

- DevelopmentのVisual Studio / VS Codeプロファイルは `http://localhost:5016` だけを使用し、開発証明書を要求しない。
- Productionは `Security:RequireHttps=true` が必須。Nginxからloopback経由で送られた `X-Forwarded-Proto: https` だけを信頼し、それ以外のKestrel直アクセスは400で拒否する。
- 80番ポートのHTTPはNginxでHTTPSへ308リダイレクトする。証明書発行前のbootstrap構成ではACME challenge以外を503にし、HTTP版アプリを公開しない。
- TLS失敗時にKestrelを外部HTTP待受へ変更して継続しない。

## 証明書の初回発行（本番サーバーで実施）

以下はドメイン、DNS、サーバー権限、停止影響を確認し、本番操作の明示許可を得た後だけ実施する。`__TIMELEDGER_DOMAIN__` は例示値のまま使わない。

1. `deploy/nginx/timeledger-bootstrap.conf.example` をNginxのサイト設定へコピーし、`__TIMELEDGER_DOMAIN__` を実ドメインへ置換する。
2. `/var/www/letsencrypt` をNginxが読み取れる状態で作成し、`nginx -t` 成功後にreloadする。
3. Certbot公式手順に従って導入し、`certbot certonly --webroot -w /var/www/letsencrypt -d <実ドメイン>` で証明書だけを発行する。秘密鍵をコピー・表示・Git登録しない。
4. `deploy/nginx/timeledger.conf.example` を同様に実ドメインへ置換して有効化し、証明書パス、`proxy_pass http://127.0.0.1:5016`、転送ヘッダーを確認する。
5. `nginx -t`、ProductionのWeb通信構成検査、Nginx reloadの順で反映する。構成検査は公開ディレクトリで次のように行い、DBへ接続せず終了する。

```bash
ASPNETCORE_ENVIRONMENT=Production \
AllowedHosts=<実ドメイン> \
dotnet TimeLedger.dll --validate-web-security
```

6. `certbot renew --dry-run`、HTTPからHTTPSへのリダイレクト、証明書SAN・発行元・有効期限・チェーン、HTTPS応答、HSTSを確認する。テストHTTPの成功を本番HTTPSの成功として扱わない。

## サーバ側デプロイ（DB変更なし）
以下は「コード更新 + 再起動」の手順。

```bash
# 1. リポジトリへ移動
cd ~/F_FinalClender

# 2. main を最新に（競合が出たら解消してから続行）
git pull origin main

# 3. Release ビルドして本番ディレクトリへ publish
dotnet publish ./TimeLedger/TimeLedger.csproj \
  -c Release \
  -o /var/www/timeledger/app

# 4. アプリ再起動
docker compose \
  --env-file ./TimeLedger/database/config/production.env \
  -f ./TimeLedger/database/compose.production.yaml \
  up -d --wait
sudo systemctl restart timeledger

# 5. 状態確認（Active: active (running) になっているか）
systemctl status timeledger
```

## サーバ側デプロイ（DBスキーマ変更あり）
以下は Migration 適用を含むリリース手順。

```bash
# 1. リポジトリへ移動
cd ~/F_FinalClender
git pull origin main

# 2. Migration を本番 DB に適用
cd ~/F_FinalClender/TimeLedger

# Migration前バックアップ
./database/scripts/database.sh production backup

set -a
source ./database/config/production.env
set +a

ASPNETCORE_ENVIRONMENT=Production \
dotnet ef database update \
  --project TimeLedger.csproj \
  --startup-project TimeLedger.csproj \
  --context ApplicationDbContext

cd ~/F_FinalClender
dotnet publish ./TimeLedger/TimeLedger.csproj \
  -c Release \
  -o /var/www/timeledger/app

sudo systemctl restart timeledger
systemctl status timeledger
```

## 運用チェックリスト
- `docker compose --env-file database/config/production.env -f database/compose.production.yaml ps` でPostgreSQLがhealthyであることを確認する。
- `database/config/production.env` と `database/runtime/` がGit管理外であり、所有者以外から読み書きできないことを確認する。
- プロジェクト配下にDB本体があるため、`git clean -x` やリポジトリディレクトリの再作成を行わない。
- 本番では固定Adminをシードしない。既存DBに旧固定資格情報由来のアカウントがある場合は、公開前に資格情報を変更し、不要なら無効化する。
- Nginxの80/443だけを外部公開し、Kestrelの5016はloopback以外から到達できないことを確認する。
- `AllowedHosts`、証明書SAN、OAuthの公開コールバックURLが同じ本番ホスト名を使用することを確認する。
- ログは標準出力に出るため、`journalctl -u timeledger` 等で収集し、PII が含まれないようログレベルを確認する。
- 外部カレンダーの OAuth トークンは暗号化未対応。公開環境では必ず暗号化ストアを用意し、移行計画を実施する。

## バックアップと復旧
- DBバックアップ: `./database/scripts/database.sh production backup`
- DB復旧: アプリの書き込みを停止し、対象ダンプを確認後に `./database/scripts/database.sh production restore <dump> --force`
- 復旧処理は上書き前の安全バックアップを `database/runtime/production/backups` に自動作成する。
- Data Protection キーをファイルや KeyVault に退避している場合は、同時にバックアップすること。
- プロジェクト配下のバックアップを別ディスクまたは承認済み保管先にも暗号化して複製する。

## ロールバック方針
- 新マイグレーション適用前に配下DBの論理バックアップを取得し、問題があれば移行元またはバックアップからリストアする。
- アプリバイナリは `/opt/timeledger/publish` をバージョン別に保持し、シンボリックリンクの切り替えで即時ロールバック可能にしておく。
- TLS構成の切戻しでも外部HTTPへ降格しない。直前の正常なNginx設定と証明書へ戻し、復旧できない場合はHTTPS側を停止して原因を保全する。
