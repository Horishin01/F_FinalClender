#!/usr/bin/env bash
set -euo pipefail

environment_name="${1:-development}"
action="${2:-status}"
backup_file="${3:-}"
force="${4:-}"

case "$environment_name" in
  development|production) ;;
  *) echo 'Environment must be development or production.' >&2; exit 2 ;;
esac

case "$action" in
  start|stop|status|backup|restore) ;;
  *) echo 'Action must be start, stop, status, backup, or restore.' >&2; exit 2 ;;
esac

script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
database_directory="$(cd -- "$script_directory/.." && pwd)"
compose_file="$database_directory/compose.$environment_name.yaml"
config_file="$database_directory/config/$environment_name.env"
runtime_directory="$database_directory/runtime/$environment_name"
data_directory="$runtime_directory/data"
backup_directory="$runtime_directory/backups"

if ! command -v docker >/dev/null 2>&1; then
  echo 'Docker Engine and the Docker Compose plugin are required.' >&2
  exit 1
fi
if [[ ! -f "$config_file" ]]; then
  echo "Missing $config_file. Copy and edit $config_file.example first." >&2
  exit 1
fi
if grep -q 'CHANGE_THIS_' "$config_file"; then
  echo "$config_file still contains an example password." >&2
  exit 1
fi

set -a
# shellcheck disable=SC1090
source "$config_file"
set +a
: "${POSTGRES_DB:?POSTGRES_DB is missing from $config_file}"
: "${POSTGRES_USER:?POSTGRES_USER is missing from $config_file}"
: "${POSTGRES_PASSWORD:?POSTGRES_PASSWORD is missing from $config_file}"

mkdir -p -- "$data_directory" "$backup_directory"
compose=(docker compose --env-file "$config_file" -f "$compose_file")

case "$action" in
  start)
    "${compose[@]}" up -d --wait
    "${compose[@]}" ps
    ;;
  stop)
    "${compose[@]}" down
    echo "Stopped. Database files remain in $data_directory"
    ;;
  status)
    "${compose[@]}" ps
    ;;
  backup)
    "${compose[@]}" up -d --wait
    timestamp="$(date -u +%Y%m%d-%H%M%S)"
    file_name="timeledger-$environment_name-$timestamp.dump"
    "${compose[@]}" exec -T postgres pg_dump -Fc --no-owner --no-privileges \
      -U "$POSTGRES_USER" -d "$POSTGRES_DB" -f "/backups/$file_name"
    echo "Created $backup_directory/$file_name"
    ;;
  restore)
    if [[ "$force" != '--force' ]]; then
      echo 'Restore overwrites the database. Re-run with: <backup-file> --force' >&2
      exit 2
    fi
    if [[ -z "$backup_file" || ! -f "$backup_file" ]]; then
      echo 'A backup file under the environment backup directory is required.' >&2
      exit 2
    fi

    resolved_backup="$(realpath -- "$backup_file")"
    resolved_backup_directory="$(realpath -- "$backup_directory")"
    case "$resolved_backup" in
      "$resolved_backup_directory"/*) ;;
      *) echo "Backup file must be under $backup_directory" >&2; exit 2 ;;
    esac

    "${compose[@]}" up -d --wait
    timestamp="$(date -u +%Y%m%d-%H%M%S)"
    safety_file="before-restore-$environment_name-$timestamp.dump"
    "${compose[@]}" exec -T postgres pg_dump -Fc --no-owner --no-privileges \
      -U "$POSTGRES_USER" -d "$POSTGRES_DB" -f "/backups/$safety_file"
    "${compose[@]}" exec -T postgres pg_restore --clean --if-exists \
      --no-owner --no-privileges --exit-on-error \
      -U "$POSTGRES_USER" -d "$POSTGRES_DB" "/backups/$(basename -- "$resolved_backup")"
    echo "Restored database. Pre-restore backup: $backup_directory/$safety_file"
    ;;
esac
