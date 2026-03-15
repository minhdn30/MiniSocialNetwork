#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd -- "${SCRIPT_DIR}/.." && pwd)"
BACKUP_SCRIPT="${PROJECT_DIR}/scripts/postgres-backup.sh"
BACKUP_DIR="${BACKUP_DIR:-/opt/backups/postgres}"
SCHEDULE="${BACKUP_SCHEDULE:-15 3 * * *}"
CRON_FILE="/etc/cron.d/cloudm-postgres-backup"
LOG_FILE="/var/log/cloudm-postgres-backup.log"

if [[ ! -x "${BACKUP_SCRIPT}" ]]; then
  chmod +x "${BACKUP_SCRIPT}"
fi

mkdir -p "${BACKUP_DIR}"
touch "${LOG_FILE}"
chmod 640 "${LOG_FILE}"

cat > "${CRON_FILE}" <<EOF
SHELL=/bin/bash
PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin

${SCHEDULE} root BACKUP_DIR=${BACKUP_DIR} ${BACKUP_SCRIPT} >> ${LOG_FILE} 2>&1
EOF

chmod 644 "${CRON_FILE}"

echo "Installed daily backup cron: ${CRON_FILE}"
echo "Schedule: ${SCHEDULE}"
echo "Backup directory: ${BACKUP_DIR}"
echo "Log file: ${LOG_FILE}"
