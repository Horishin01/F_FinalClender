# デプロイ手順（Ubuntu テスト環境 / 更新日: 2026-02-13）

## 前提
- テスト環境 OS は Ubuntu（22.04 LTS / 24.04 LTS）を想定。
- サーバーに .NET 8.0 ランタイムと PostgreSQL 14+ がインストール済み。
- 逆プロキシ (例: Nginx/Apache) で TLS 終端し、アプリは Kestrel で稼働させる想定。
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
- `ASPNETCORE_URLS=https://0.0.0.0:7052;http://0.0.0.0:5016`（必要に応じて変更）
- `Authentication__Outlook__ClientId` / `Authentication__Outlook__ClientSecret`（Outlook 連携を使う場合）
- `Authentication__Google__ClientId` / `Authentication__Google__ClientSecret`（Google 連携を使う場合）
- Data Protection キーの永続化先（ファイル共有や KeyVault 等）を環境変数や設定で指定することを推奨。

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
ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=timeledger_db;Username=timeledger_user;Password=i2JvwXGn!;" \
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
- 初回起動後、シードされた Admin のパスワードを必ず変更し、不要なら Seed を無効化する。
- ログは標準出力に出るため、`journalctl -u timeledger` 等で収集し、PII が含まれないようログレベルを確認する。
- 外部カレンダーの OAuth トークンは暗号化未対応。公開環境では必ず暗号化ストアを用意し、移行計画を実施する。

## バックアップと復旧
- DB バックアップ: `pg_dump -Fc -h <host> -U <user> <database> > backup.dump`
- 復旧: `pg_restore -c -d <database> backup.dump`
- Data Protection キーをファイルや KeyVault に退避している場合は、同時にバックアップすること。

## ロールバック方針
- 新マイグレーション適用前に DB バックアップを取得し、問題があればバックアップからリストアする。
- アプリバイナリは `/opt/timeledger/publish` をバージョン別に保持し、シンボリックリンクの切り替えで即時ロールバック可能にしておく。
