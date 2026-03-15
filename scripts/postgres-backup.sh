#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd -- "${SCRIPT_DIR}/.." && pwd)"
ENV_FILE="${PROJECT_DIR}/.env.production"
BACKUP_DIR="${BACKUP_DIR:-/opt/backups/postgres}"
RETENTION_COUNT="${RETENTION_COUNT:-7}"
TIMESTAMP="$(date +"%Y-%m-%d-%H%M%S")"

if [[ ! -f "${ENV_FILE}" ]]; then
  echo "Missing .env.production at ${ENV_FILE}" >&2
  exit 1
fi

set -a
source "${ENV_FILE}"
set +a

DB_NAME="${POSTGRES_DB:-cloudm}"
TMP_FILE="${BACKUP_DIR}/${DB_NAME}-${TIMESTAMP}.sql.gz.tmp"
FINAL_FILE="${BACKUP_DIR}/${DB_NAME}-${TIMESTAMP}.sql.gz"

mkdir -p "${BACKUP_DIR}"

cd "${PROJECT_DIR}"

docker compose --env-file .env.production -f docker-compose.prod.yml exec -T postgres sh -lc \
  'PGPASSWORD="$POSTGRES_PASSWORD" pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" --no-owner --no-privileges' \
  | gzip -9 > "${TMP_FILE}"

mv "${TMP_FILE}" "${FINAL_FILE}"

find "${BACKUP_DIR}" -maxdepth 1 -type f -name "${DB_NAME}-*.sql.gz" -printf '%T@ %p\n' \
  | sort -nr \
  | awk -v keep="${RETENTION_COUNT}" 'NR > keep { print $2 }' \
  | xargs -r rm -f

echo "Backup created: ${FINAL_FILE}"
