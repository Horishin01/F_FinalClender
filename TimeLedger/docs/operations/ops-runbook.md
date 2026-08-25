# 運用 Runbook（更新日: 2026-08-25）

## 対象環境
- Ubuntu サーバー
- systemd サービス名: `timeledger`
- デプロイ先: `/var/www/timeledger/app`
- リポジトリ: `~/F_FinalClender`

## 目的
- 障害時に短時間で「アプリ起動問題」「DB問題」「外部連携問題」を切り分ける。

## 基本コマンド
- ステータス: `sudo systemctl status timeledger`
- 再起動: `sudo systemctl restart timeledger`
- ログ追跡: `sudo journalctl -u timeledger -n 200 -f`
- プロセス確認: `ss -lntp | grep dotnet`
- Nginx構文確認: `sudo nginx -t`
- Nginx状態: `sudo systemctl status nginx`
- 証明書一覧: `sudo certbot certificates`
- 自動更新試験: `sudo certbot renew --dry-run`
- DB状態: `./database/scripts/database.sh production status`
- DBバックアップ: `./database/scripts/database.sh production backup`

## デプロイ直後の確認
- `systemctl status timeledger` で `active (running)` を確認。
- `curl -I https://<host>/` で `200` または `302` を確認。
- `curl -I http://<host>/` が同じホストのHTTPSへ308で遷移することを確認。
- 証明書のSAN、発行元、期限、チェーンが接続ホストと一致し、ブラウザ警告がないことを確認。
- `ss -lntp` でKestrelの5016が `127.0.0.1` だけに待ち受け、外部インターフェースへ公開されていないことを確認。
- ログにマイグレーション/接続文字列エラーが出ていないことを確認。

## アプリ別ヘルスチェック
- `カレンダー（Events）`
- ログイン後にイベント一覧が表示されること。
- `Sync` 実行で 500 が連続しないこと。

- `外部連携（Outlook/Google）`
- 連携状態ページで接続状態が読み出せること。
- 同期実行時に `LinkRequired` へ誤遷移しないこと（連携済みの場合）。

- `iCloud`
- 管理者アカウントで iCloud 設定ページを開けること。
- 同期実行時に認証エラーが続く場合は Apple ID / アプリパスワードを再確認。

- `ICカード`
- 依存確認: `ldconfig -p | grep libpcsclite.so.1`
- サービス確認: `sudo systemctl status pcscd --no-pager`
- 読み取り失敗時は `pcscd` と USB リーダー接続状態を先に確認。

## DB確認
- 配置: DB本体は `database/runtime/production/data`、論理バックアップは `database/runtime/production/backups`、秘密設定は `database/config/production.env`。
- コンテナ状態: `docker compose --env-file database/config/production.env -f database/compose.production.yaml ps`
- 接続試験: `docker compose --env-file database/config/production.env -f database/compose.production.yaml exec -T postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "select 1"'`
- バックアップ: `./database/scripts/database.sh production backup`
- 復旧: アプリ停止後、`./database/scripts/database.sh production restore <dump> --force`

## 典型障害と一次対応
- `起動失敗`
- `journalctl` で接続文字列・権限・ポート競合を確認し、DBコンテナがhealthyか確認。
- 必要に応じて `dotnet publish` の出力先権限を再確認。

- `DBコンテナ起動失敗`
- `docker compose ... logs postgres` で初期化・権限・ポート競合を確認する。
- `database/config/production.env` の `POSTGRES_*` と `ConnectionStrings__DefaultConnection` が一致しているか確認する。値自体はログやチケットへ貼り付けない。
- PostgreSQLメジャーバージョンが物理データと異なる場合は直接起動せず、論理バックアップまたは `pg_upgrade` で移行する。

- `TIMELEDGER-PRODUCTION-HTTPS-REQUIRED` / `TIMELEDGER-PRODUCTION-HOSTS-REQUIRED`
- `ASPNETCORE_ENVIRONMENT=Production`、`Security:RequireHttps=true`、`AllowedHosts`が実ドメインであることを確認する。検査を無効化したりワイルドカードへ緩和したりしない。

- `TIMELEDGER-TRUSTED-PROXY-INVALID` またはHTTPS経由でも400
- NginxとKestrelが同一ホストで、proxy先が `127.0.0.1:5016`、`X-Forwarded-Proto https` が設定されていることを確認する。未知プロキシを一括信頼しない。

- デバッグ時に証明書警告が出る
- `http` 起動プロファイルまたはVS Codeの `http://localhost:5016` を使用する。テスト用HTTPをProductionへ流用しない。

- 証明書期限切れ・更新失敗
- 外部HTTPへ降格せず、Nginxログ、Certbot timer、DNS、80番challenge到達性を確認する。更新後は `nginx -t` とreload、実ブラウザ確認を行う。

- `ログイン後に管理画面が見えない`
- 対象ユーザーのロール（Admin）付与状態を確認。

- `Outlook/Google 同期失敗`
- OAuth クライアントID/Secretの設定値を確認。
- トークン期限切れなら再連携を実施。

- `iCloud 同期失敗`
- Apple ID / アプリパスワード再設定、CalDAV 応答エラーをログで確認。

- `ICカード読み取り失敗`
- `pcscd` 起動状態、`libpcsclite.so.1`、カードリーダー接続を確認。

## ロールバック
- リリース前バックアップ（`database/runtime/production/backups` のDBダンプ）から復元。DB切替直後は確認完了まで移行元も保持する。
- 直前に稼働していた publish 内容へ戻して `sudo systemctl restart timeledger`。
- Nginxは直前の検証済みHTTPS設定と有効な証明書へ戻す。復旧不能時はHTTPS公開を停止し、HTTPで本番継続しない。

## エスカレーション
- P0（認証不能、データ消失、権限逸脱、トークン漏洩疑い）は即時連絡し、外部トークン失効を最優先で実施する。
