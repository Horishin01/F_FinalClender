# 運用 Runbook（更新日: 2026-02-13）

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

## デプロイ直後の確認
- `systemctl status timeledger` で `active (running)` を確認。
- `curl -I https://<host>/` で `200` または `302` を確認。
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
- 接続試験: `psql "<conn-string>" -c "select 1"`
- バックアップ: `pg_dump -Fc -h <host> -U <user> <db> > backup.dump`
- 復旧: `pg_restore -c -d <db> backup.dump`

## 典型障害と一次対応
- `起動失敗`
- `journalctl` で接続文字列・権限・ポート競合を確認。
- 必要に応じて `dotnet publish` の出力先権限を再確認。

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
- リリース前バックアップ（DBダンプ）から復元。
- 直前に稼働していた publish 内容へ戻して `sudo systemctl restart timeledger`。

## エスカレーション
- P0（認証不能、データ消失、権限逸脱、トークン漏洩疑い）は即時連絡し、外部トークン失効を最優先で実施する。
