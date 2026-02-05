#!/usr/bin/env bash
# Start Project Exchange: backend (port 5051) then frontend (port 3000).
# Run from repo root: ./scripts/start-exchange.sh
# If dotnet test fails, the start sequence is aborted immediately.

set -e
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
BACKEND_PID=""

# --- Run tests first; abort entire sequence on failure ---
echo "[Project Exchange] Running tests..."
dotnet test --verbosity minimal
echo "[Project Exchange] All tests passed."

# --- Nuclear Option: force kill everything on frontend/backend ports ---
echo "[Project Exchange] Nuclear Option: freeing ports 3000–3004 and 5051..."
for port in 3000 3001 3002 3003 3004 5051; do
  pids=$(lsof -i :$port -t 2>/dev/null || true)
  if [ -n "$pids" ]; then
    echo "$pids" | xargs kill -9 2>/dev/null || true
    echo "  killed process(es) on port $port"
  fi
done
sleep 2

cleanup() { [ -n "$BACKEND_PID" ] && kill $BACKEND_PID 2>/dev/null || true; exit 0; }
trap cleanup INT TERM

echo "[Project Exchange] Starting backend (http://localhost:5051)..."
dotnet run --project ProjectExchange.Core --launch-profile http &
BACKEND_PID=$!
echo "[Project Exchange] Waiting for backend to be ready..."
sleep 4

echo "[Project Exchange] Starting frontend (http://localhost:3000)..."
cd "$ROOT/project-exchange-frontend"
# Clear Next.js and Turbopack caches to avoid "corrupted database or bug" / missing .sst file errors
rm -rf .next node_modules/.cache
# Use webpack dev server to avoid Turbopack cache panics; use 'npm run dev' for Turbopack.
npm run dev:webpack

cleanup
