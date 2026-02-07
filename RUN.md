# Running Project Exchange

All commands below assume you open a terminal and run them **from the repo root** (`projectexchange`). If you are elsewhere, run the `cd` in the same block first.

## Database (PostgreSQL)

The backend needs PostgreSQL on **localhost:5432** (database `projectexchange`, user `postgres`, password `postgres`). If you see `NpgsqlException: Failed to connect to 127.0.0.1:5432`:

1. **Start Postgres with Docker** (recommended; uses `docker-compose.yml`):
   ```bash
   cd /Users/ddpp/Desktop/projectexchange
   chmod +x scripts/start-database.sh
   ./scripts/start-database.sh
   ```
   This runs `docker compose up -d postgres` and waits until the DB is ready.

2. **Or start Postgres with Homebrew** (if you prefer local Postgres):
   ```bash
   brew services start postgresql@16
   # Or: brew services start postgresql
   ```
   Then create the database and user if needed:
   ```bash
   createuser -s postgres 2>/dev/null || true
   createdb -O postgres projectexchange 2>/dev/null || true
   ```

3. **Check that Postgres is listening:**
   ```bash
   lsof -i :5432
   ```
   You should see a process (e.g. `postgres` or `com.docker`).

4. **Then start the exchange** so migrations run:
   ```bash
   ./scripts/start-exchange.sh
   ```

---

## Start everything (backend + frontend in one go)

From repo root:

```bash
cd /Users/ddpp/Desktop/projectexchange
chmod +x scripts/start-exchange.sh
./scripts/start-exchange.sh
```

- Backend: http://localhost:5051 (Swagger: http://localhost:5051/swagger)
- Frontend: http://localhost:3000 (or next free port, e.g. 3002, if 3000 is in use)

If **port 5051 is already in use**, the script skips starting the backend and uses the existing one. If **port 3000 is in use**, Next.js starts on the next available port (e.g. 3002) — check the terminal for the actual URL.

To stop: press Ctrl+C (stops frontend; if the script started the backend, it is stopped too).

---

## Start only frontend (backend already running on 5051)

From repo root:

```bash
cd /Users/ddpp/Desktop/projectexchange/project-exchange-frontend
npm run dev:clean
```

Or with standard dev (may hit lock if another instance ran):

```bash
cd /Users/ddpp/Desktop/projectexchange/project-exchange-frontend
npm run dev
```

---

## Start only backend

From repo root:

```bash
cd /Users/ddpp/Desktop/projectexchange
dotnet run --project ProjectExchange.Core --launch-profile http
```

---

## Clean start (free ports 5051 and 3000 first)

If you want to stop whatever is using the ports, then start:

```bash
cd /Users/ddpp/Desktop/projectexchange
# Kill processes on 5051 and 3000 (macOS/Linux)
for port in 5051 3000; do pids=$(lsof -i :$port -t 2>/dev/null); [ -n "$pids" ] && echo "$pids" | xargs kill 2>/dev/null; done
sleep 1
./scripts/start-exchange.sh
```

---

## If something doesn’t work

- **Trade History empty or error on dashboard**: The widget shows the API error message directly. No need to “check the terminal” — read the message under “Trade History” (e.g. “API: Failed to fetch” or “API error: 404”).
- **Port in use**: Stop the other process or use a different profile (e.g. `http-5050` for port 5050).

### Dashboard shows "Internal Server Error" (500)

The 500 comes from the **Next.js frontend**, not the .NET backend. In the terminal you may see:

- `Cannot find module '.../middleware-manifest.json'`
- `ENOENT: no such file or directory, open '.../routes-manifest.json'`
- `GET / 500 in 11.8s`

**Fix:** The `.next` build folder is corrupted or incomplete. Clean it and restart the frontend:

```bash
cd /Users/ddpp/Desktop/projectexchange
chmod +x scripts/start-frontend-clean.sh
./scripts/start-frontend-clean.sh
```

Or manually:

```bash
cd project-exchange-frontend
rm -rf .next node_modules/.cache
npm run dev:clean
# or: npm run dev:webpack
```

Then open http://localhost:3000 again (and ensure the backend is running on 5051 if the dashboard calls the API).

**If you see** `Persisting failed: Unable to write SST file` (Turbopack cache): the app may still work; open http://localhost:3000/dashboard (or the port Next chose, e.g. 3001). If you get 500 or odd behavior, use Webpack instead: `npm run dev:webpack`. To try avoiding the SST error with Turbopack, run `npm run dev:safe` (ensures `.next` exists before starting).

**If port 3000 is in use:** Next will use 3001 (or the next free port). Check the terminal for the actual URL, e.g. `Local: http://localhost:3001`.

### Tests and frontend

- Run tests: `dotnet test` (includes PositionServiceTests). Filter: `dotnet test --filter "FullyQualifiedName~PositionServiceTests"`.
- Frontend: `npm start` (production, after `npm run build`) or `npm run dev` (development).
