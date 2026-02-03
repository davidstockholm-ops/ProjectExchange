# Resolve market via POST /api/admin/resolve-market.
# Usage: bash scripts/resolve-market.sh [settlementAccountId]
#   Or:  ACCOUNT_ID=your-guid bash scripts/resolve-market.sh
# Port 5052 = https profile; override with RESOLVE_PORT=5051 if needed.

set -e
PORT="${RESOLVE_PORT:-5052}"
BASE_URL="http://localhost:${PORT}"

ACCOUNT_ID="${ACCOUNT_ID:-$1}"
if [ -z "$ACCOUNT_ID" ]; then
  ACCOUNT_ID="3fa85f64-5717-4562-b3fc-2c9c63f6afa6"
  echo "Using default settlement account ID (create a wallet via POST /api/wallet/create to get a real one)."
fi

echo "Calling resolve-market (outcomeId=drake-album, winningAssetType=DRAKE_WIN, settlementAccountId=$ACCOUNT_ID)..."
curl -s -X POST "${BASE_URL}/api/admin/resolve-market" \
  -H "Content-Type: application/json" \
  -d "{\"outcomeId\":\"drake-album\",\"winningAssetType\":\"DRAKE_WIN\",\"settlementAccountId\":\"$ACCOUNT_ID\",\"usdPerToken\":1.0}"

echo ""
echo "Done. If you see connection refused, start the app: dotnet run --project ProjectExchange.Core --launch-profile https"
