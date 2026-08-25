# 配備ファイル

- `nginx/`: TLS終端とKestrelへのリバースプロキシ設定例。
- `systemd/`: Productionアプリを起動するsystemdユニット例。
- PostgreSQL本体、接続設定、バックアップは `../database/` に集約する。

`.example` はそのまま使用せず、プレースホルダーを実環境のパス・ドメインへ置換してから構文検査する。秘密値、証明書、秘密鍵、DBデータはGitへ追加しない。
