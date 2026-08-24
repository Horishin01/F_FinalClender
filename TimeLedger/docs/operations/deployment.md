# デプロイ手順（Ubuntu / 更新日: 2026-08-25）

## 前提
- テスト環境 OS は Ubuntu（22.04 LTS / 24.04 LTS）を想定。
- サーバーに .NET 8.0 ランタイムと PostgreSQL 14+ がインストール済み。
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

## 必須設定（環境変数推奨）
- `ConnectionStrings__DefaultConnection` : PostgreSQL 接続文字列
- `ASPNETCORE_ENVIRONMENT=Production`
- `AllowedHosts=<証明書のSANと一致する実ドメイン>`
- Kestrelの既定本番待受は `appsettings.Production.json` の `http://127.0.0.1:5016`。`ASPNETCORE_URLS` で公開IPやHTTPSへ変更しない。
- `ASPNETCORE_PATHBASE=/timeledger`（サブパス配信時のみ。例: `https://example.com/timeledger`）
- `Authentication__Outlook__ClientId` / `Authentication__Outlook__ClientSecret`（Outlook 連携を使う場合）
- `Authentication__Google__ClientId` / `Authentication__Google__ClientSecret`（Google 連携を使う場合）
- Data Protection キーの永続化先（ファイル共有や KeyVault 等）を環境変数や設定で指定することを推奨。

接続文字列、OAuth秘密情報、初期管理者情報はリポジトリ内のJSON、systemdユニット本文、Markdownへ書かず、所有者だけが読める `/etc/timeledger/timeledger.env` などの外部環境ファイルまたは承認済み秘密情報ストアへ置く。本番では `BootstrapAdmin__Enabled=true` を拒否する。

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
- 本番では固定Adminをシードしない。既存DBに旧固定資格情報由来のアカウントがある場合は、公開前に資格情報を変更し、不要なら無効化する。
- Nginxの80/443だけを外部公開し、Kestrelの5016はloopback以外から到達できないことを確認する。
- `AllowedHosts`、証明書SAN、OAuthの公開コールバックURLが同じ本番ホスト名を使用することを確認する。
- ログは標準出力に出るため、`journalctl -u timeledger` 等で収集し、PII が含まれないようログレベルを確認する。
- 外部カレンダーの OAuth トークンは暗号化未対応。公開環境では必ず暗号化ストアを用意し、移行計画を実施する。

## バックアップと復旧
- DB バックアップ: `pg_dump -Fc -h <host> -U <user> <database> > backup.dump`
- 復旧: `pg_restore -c -d <database> backup.dump`
- Data Protection キーをファイルや KeyVault に退避している場合は、同時にバックアップすること。

## ロールバック方針
- 新マイグレーション適用前に DB バックアップを取得し、問題があればバックアップからリストアする。
- アプリバイナリは `/opt/timeledger/publish` をバージョン別に保持し、シンボリックリンクの切り替えで即時ロールバック可能にしておく。
- TLS構成の切戻しでも外部HTTPへ降格しない。直前の正常なNginx設定と証明書へ戻し、復旧できない場合はHTTPS側を停止して原因を保全する。
