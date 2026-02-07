#!/usr/bin/env bash
# Start PostgreSQL for Project Exchange (Docker). Backend expects localhost:5432.
# Run from repo root: ./scripts/start-database.sh

set -e
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

if lsof -i :5432 -t >/dev/null 2>&1; then
  echo "[Project Exchange] Port 5432 already in use — PostgreSQL is likely running."
  exit 0
fi

echo "[Project Exchange] Starting PostgreSQL via Docker Compose..."
docker compose up -d postgres

echo "[Project Exchange] Waiting for Postgres to accept connections..."
for i in 1 2 3 4 5 6 7 8 9 10; do
  if docker compose exec -T postgres pg_isready -U postgres -d projectexchange >/dev/null 2>&1; then
    echo "[Project Exchange] PostgreSQL is ready (localhost:5432, database=projectexchange, user=postgres)."
    exit 0
  fi
  sleep 1
done

echo "[Project Exchange] WARNING: Postgres may still be starting. Run: docker compose logs postgres"
exit 0
