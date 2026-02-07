#!/usr/bin/env bash
# Fix "Internal Server Error" / 500 on dashboard when Next.js .next is corrupted or missing manifests.
# Run from repo root: ./scripts/start-frontend-clean.sh

set -e
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT/project-exchange-frontend"

echo "[Project Exchange] Cleaning Next.js cache and .next..."
rm -rf .next node_modules/.cache

echo "[Project Exchange] Starting frontend (http://localhost:3000)..."
npm run dev:webpack
