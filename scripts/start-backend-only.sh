#!/usr/bin/env bash
# Start only the backend (port 5051). Use in Terminal 1; then run frontend in Terminal 2.
# Run from repo root: ./scripts/start-backend-only.sh

set -e
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

echo "[Project Exchange] Starting backend (http://localhost:5051)..."
exec dotnet run --project ProjectExchange.Core --launch-profile http
