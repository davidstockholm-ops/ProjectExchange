# Running Project Exchange

All commands below assume you open a terminal and run them **from the repo root** (`projectexchange`). If you are elsewhere, run the `cd` in the same block first.

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
