#!/usr/bin/env bash
# Run from project-exchange-frontend after full cache/node_modules removal.
# 3. npm install
# 4. npx next build
# 5. If build OK, npm run dev:webpack

set -e
cd "$(dirname "$0")"

echo "[1/3] npm install..."
npm install

echo "[2/3] npx next build..."
npx next build

echo "[3/3] Starting dev server (webpack)..."
npm run dev:webpack
