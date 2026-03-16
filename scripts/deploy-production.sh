#!/usr/bin/env bash

set -euo pipefail

PROJECT_DIR="${PROJECT_DIR:-$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)}"
ENV_FILE="${PROJECT_DIR}/.env.production"
DEPLOY_BRANCH="${DEPLOY_BRANCH:-main}"
HEALTHCHECK_ATTEMPTS="${HEALTHCHECK_ATTEMPTS:-20}"
HEALTHCHECK_DELAY_SECONDS="${HEALTHCHECK_DELAY_SECONDS:-5}"

if [[ ! -f "${ENV_FILE}" ]]; then
  echo "Missing .env.production at ${ENV_FILE}" >&2
  exit 1
fi

set -a
source "${ENV_FILE}"
set +a

API_HEALTH_URL="${API_HEALTH_URL:-https://${API_DOMAIN}/swagger/index.html}"

cd "${PROJECT_DIR}"

git config --global --add safe.directory "${PROJECT_DIR}"
git fetch origin "${DEPLOY_BRANCH}"

CURRENT_BRANCH="$(git rev-parse --abbrev-ref HEAD)"
if [[ "${CURRENT_BRANCH}" != "${DEPLOY_BRANCH}" ]]; then
  git checkout "${DEPLOY_BRANCH}"
fi

git pull --ff-only origin "${DEPLOY_BRANCH}"

docker compose --env-file .env.production -f docker-compose.prod.yml --profile tools run --rm migrator
docker compose --env-file .env.production -f docker-compose.prod.yml up -d --build

for ((attempt = 1; attempt <= HEALTHCHECK_ATTEMPTS; attempt++)); do
  if curl -fsS "${API_HEALTH_URL}" >/dev/null; then
    echo "Health check passed: ${API_HEALTH_URL}"
    docker compose --env-file .env.production -f docker-compose.prod.yml ps
    exit 0
  fi

  if (( attempt < HEALTHCHECK_ATTEMPTS )); then
    sleep "${HEALTHCHECK_DELAY_SECONDS}"
  fi
done

echo "Health check failed: ${API_HEALTH_URL}" >&2
docker compose --env-file .env.production -f docker-compose.prod.yml logs api --tail=100 >&2
docker compose --env-file .env.production -f docker-compose.prod.yml logs caddy --tail=100 >&2
exit 1
