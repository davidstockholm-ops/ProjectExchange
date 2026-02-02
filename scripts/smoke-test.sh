#!/usr/bin/env bash
# General smoke test for Project Exchange API.
# Run with the API already running (e.g. dotnet run --project ProjectExchange.Core).
# Usage: ./scripts/smoke-test.sh [BASE_URL]
# Example: ./scripts/smoke-test.sh http://localhost:5050

set -e
BASE_URL="${1:-http://localhost:5050}"
FAIL=0

echo "Smoke test: $BASE_URL"
echo "---"

# 1. Health
echo -n "GET /health ... "
CODE=$(curl -s -o /tmp/smoke-health.json -w "%{http_code}" "$BASE_URL/health")
if [ "$CODE" = "200" ] && grep -q '"status":"ok"' /tmp/smoke-health.json 2>/dev/null; then
  echo "OK ($CODE)"
else
  echo "FAIL ($CODE)"
  FAIL=1
fi

# 2. Create Flash market
echo -n "POST /api/markets/flash/create ... "
CODE=$(curl -s -o /tmp/smoke-flash.json -w "%{http_code}" -X POST "$BASE_URL/api/markets/flash/create" \
  -H "Content-Type: application/json" \
  -d '{"title":"Smoke Test Flash","durationMinutes":5}')
if [ "$CODE" = "200" ] && grep -q '"outcomeId"' /tmp/smoke-flash.json 2>/dev/null; then
  echo "OK ($CODE)"
  OUTCOME_ID=$(grep -o '"outcomeId":"[^"]*"' /tmp/smoke-flash.json | head -1 | cut -d'"' -f4)
else
  echo "FAIL ($CODE)"
  FAIL=1
fi

# 3. Get active markets
echo -n "GET /api/markets/active ... "
CODE=$(curl -s -o /tmp/smoke-active.json -w "%{http_code}" "$BASE_URL/api/markets/active")
if [ "$CODE" = "200" ]; then
  echo "OK ($CODE)"
else
  echo "FAIL ($CODE)"
  FAIL=1
fi

# 4. Get order book (if we have an outcomeId)
if [ -n "$OUTCOME_ID" ]; then
  echo -n "GET /api/markets/orderbook/{outcomeId} ... "
  CODE=$(curl -s -o /tmp/smoke-book.json -w "%{http_code}" "$BASE_URL/api/markets/orderbook/$OUTCOME_ID")
  if [ "$CODE" = "200" ]; then
    echo "OK ($CODE)"
  else
    echo "FAIL ($CODE)"
    FAIL=1
  fi
fi

# 5. Outcome reached (idempotent; may return "no clearing transactions")
echo -n "POST /api/markets/outcome-reached ... "
CODE=$(curl -s -o /tmp/smoke-outcome.json -w "%{http_code}" -X POST "$BASE_URL/api/markets/outcome-reached" \
  -H "Content-Type: application/json" \
  -d "{\"outcomeId\":\"${OUTCOME_ID:-outcome-none}\"}")
if [ "$CODE" = "200" ]; then
  echo "OK ($CODE)"
else
  echo "FAIL ($CODE)"
  FAIL=1
fi

echo "---"
if [ $FAIL -eq 0 ]; then
  echo "PASS: All smoke tests passed."
  exit 0
else
  echo "FAIL: One or more smoke tests failed."
  exit 1
fi
