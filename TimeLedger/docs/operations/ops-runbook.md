# 運用 Runbook（更新日: 2026-02-12）

## 目的
本番/ステージングで障害が起きた際に、短時間で原因を切り分け復旧するための手順メモ。

## よく使うコマンド（例: systemd サービス名を `timeledger` とする場合）
- ステータス確認: `sudo systemctl status timeledger`
- 起動/停止/再起動: `sudo systemctl start|stop|restart timeledger`
- ログ確認: `sudo journalctl -u timeledger -n 200 -f`
- 公開ポート確認: `ss -lntp | grep dotnet`

## ヘルスチェック
- Web: 200 が返ることを確認 (`curl -I https://<host>/` → 200/302)。認証必須のためログイン画面または Home への 302 で OK。
- DB: `psql "<conn-string>" -c "select 1"` で応答を確認。
- 外部カレンダー連携: 管理画面で Outlook/Google の状態を確認し、`ExternalCalendars/Sync` ボタンで同期レスポンスをチェック。

## データベース
- バックアップ: `pg_dump -Fc -h <host> -U <user> <db> > backup.dump`
- 復元: `pg_restore -c -d <db> backup.dump`
- マイグレーション適用: `dotnet ef database update --project TimeLedger/TimeLedger.csproj --configuration Release`

## 典型的な障害と確認ポイント
- **起動しない / すぐ終了する**: `journalctl` で接続文字列エラーやポート占有を確認。`ConnectionStrings__DefaultConnection` と DB 到達性を再確認。
- **ログイン不可**: DB 応答、Email 確認ポリシー、ロール設定を確認。Admin シードを誤って削除した場合は一時的に Seed コードを有効にして再起動。
- **外部カレンダー同期失敗**: トークン期限切れ・スコープ不足が多い。OAuth 設定値、`ExternalCalendar...` テーブルのトークン有効期限を確認し、再連携を促す。
- **iCloud 同期遅延/失敗**: CalDAV はレート制限（60 秒クールダウン）あり。ログにエラーがないか確認し、Apple ID/アプリパスワードが変更されていないか確認。

## ロールバック
- デプロイ前に取得した DB バックアップへリストアし、前バージョンの publish ディレクトリに切り替える（シンボリックリンク運用が便利）。

## 連絡・エスカレーション
- セキュリティインシデントや P0 障害は関係者（運用/セキュリティ担当）へ即時連絡し、外部プロバイダーのトークン失効を最優先で実施する。
