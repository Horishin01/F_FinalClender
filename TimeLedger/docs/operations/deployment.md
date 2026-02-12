# デプロイ手順（更新日: 2026-02-12）

## 前提
- サーバーに .NET 8.0 ランタイムと PostgreSQL 14+ がインストール済み。
- 逆プロキシ (例: Nginx/Apache) で TLS 終端し、アプリは Kestrel で稼働させる想定。

## 必須設定（環境変数推奨）
- `ConnectionStrings__DefaultConnection` : PostgreSQL 接続文字列
- `ASPNETCORE_ENVIRONMENT=Production`
- `ASPNETCORE_URLS=https://0.0.0.0:7052;http://0.0.0.0:5016`（必要に応じて変更）
- `Authentication__Outlook__ClientId` / `Authentication__Outlook__ClientSecret`（Outlook 連携を使う場合）
- `Authentication__Google__ClientId` / `Authentication__Google__ClientSecret`（Google 連携を使う場合）
- Data Protection キーの永続化先（ファイル共有や KeyVault 等）を環境変数や設定で指定することを推奨。

## デプロイの流れ
1) リポジトリを取得し、`dotnet restore` を実行。  
2) ビルド: `dotnet publish -c Release -o /opt/timeledger/publish`  
3) DB マイグレーション:  
   `dotnet ef database update --project TimeLedger/TimeLedger.csproj --configuration Release -- --ConnectionStrings:DefaultConnection="..."`
4) サービス起動（例: systemd）  
   - `ExecStart=/usr/bin/dotnet /opt/timeledger/publish/TimeLedger.dll`  
   - 環境変数は unit ファイルまたは `/etc/environment` で設定。  
5) 逆プロキシを設定し、HTTPS で公開。HSTS は `UseHsts()` が有効。

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
