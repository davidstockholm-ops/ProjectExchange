# Project Exchange — Developer Guide

This document describes the architecture, core modules, and development practices for **Project Exchange**: a clearing and settlement platform with double-entry accounting, order matching, and celebrity-driven (Drake) markets.

---

## Architecture Overview

### Clearing & Settlement Split

Project Exchange separates **clearing** (internal obligation and matching) from **settlement** (external payment and finality):

- **Clearing**: Order-matching and virtual accounting. Trades are recorded as internal debits/credits in the ledger. No external money moves until settlement.
- **Settlement**: When an outcome is reached or a contract matures, the Auto-Settlement Agent posts Settlement-phase transactions that reference clearing transactions. A separate Settlement Gateway (and Wallet Proxy) can then issue external payment instructions.

This split keeps matching and risk logic inside the exchange while leaving payment rails and B2C wallet integration to dedicated components.

### Double-Entry Ledger System

All financial state is represented in a **double-entry ledger**:

- **Accounts** are scoped by operator (user) and type (e.g. Asset).
- **Transactions** group one or more **Journal Entries**. Each entry has an account, amount, type (Debit/Credit), and settlement phase (Clearing or Settlement).
- Every transaction is balanced: total debits = total credits. The ledger enforces this invariant so that the system-wide sum of signed amounts (Debit positive, Credit negative) is always zero.

Persistence uses EF Core with PostgreSQL in production and SQLite in-memory for tests. The `ProjectExchangeDbContext` maps domain entities to `AccountEntity`, `TransactionEntity`, and `JournalEntryEntity`.

---

## Core Modules

### MarketService

- **Responsibility**: Place orders, run matching, and record each match as a Trade in the ledger.
- **Flow**: Orders are added to an `OrderBook` (per outcome). `MatchOrders()` fills at resting order price; for each match, the service posts a double-entry Trade (buyer Debit, seller Credit) in a single `IDbContextTransaction` so that matching and ledger updates succeed or fail together.
- **Validation**: Buyer balance is checked before applying a match; insufficient funds throw `InsufficientFundsException`. Optional `IOutcomeRegistry` restricts orders to registered (opened) outcomes; invalid outcome throws `InvalidOutcomeException`.
- **Copy-trading**: If the order belongs to a “Master” with followers, `CopyTradingService.MirrorOrderAsync` builds mirrored orders (same outcome, price, type; default quantity) and each is placed via `PlaceOrderAsync` in turn.

### LedgerService

- **Responsibility**: Calculate balances and post transactions.
- **Balance**: `GetAccountBalanceAsync(accountId, phase)` sums Debits minus Credits for that account, optionally filtered by `SettlementPhase` (Clearing vs Settlement).
- **Posting**: `PostTransactionAsync(entries)` appends a balanced transaction via `ITransactionRepository` and commits via `IUnitOfWork`. Used for funding, manual adjustments, and settlement-phase reverses.

### Copy-Trading Engine

- **CopyTradingService**: In-memory follow graph (Master → Followers). `Follow(followerId, masterId)` and `MirrorOrderAsync(masterOrder)` produce mirrored orders for all followers of the master.
- **CopyTradingEngine**: Subscribes to `IOutcomeOracle.TradeProposed` (e.g. `CelebrityOracleService`). When a celebrity “trades,” it resolves a scoped `LedgerService` and posts the trade to the ledger (debit celebrity Main Operating Account per actor, credit Market Holding Account), and tracks clearing transaction IDs per outcome for the Auto-Settlement Agent.

---

## The Drake Integration

### Outcome-as-an-Asset

- **IOutcomeOracle** (implemented by **CelebrityOracleService**) creates market events per actor: `CreateMarketEvent(actorId, title, type, durationMinutes)`. Each event gets a unique `OutcomeId` and tracks `ActorId` (e.g. "Drake", "Elon") and `ResponsibleOracleId` for settlement. The same oracle can host markets for multiple celebrities; the order book per outcome is registered so users can trade “outcome-x” as an asset.
- **OutcomeRegistry** (optional): When provided, only registered outcomes accept orders. The Oracle registers the outcome when the market is opened; `MarketService` rejects orders for unknown outcomes with `InvalidOutcomeException`.

### Auto-Settlement Agent

- **Role**: When an outcome is *reached* (e.g. event resolved), the agent settles related clearing trades by posting **Settlement-phase** transactions that reverse or close the clearing entries, referencing the original clearing transaction IDs.
- **Idempotency**: Cleared transaction IDs are tracked; already-settled clearing transactions are skipped.
- **Agentic AI readiness**: `SettleOutcomeAsync` accepts optional **ConfidenceScore** (e.g. 0.0–1.0) and **SourceVerificationList** (URLs or identifiers of sources used to verify the outcome). The API and `SettlementResult` echo these back so the system reports not just the result but how sure the agent is and which sources it used.
- **Integration**: Uses `IServiceScopeFactory` to obtain scoped `LedgerService` and `ITransactionRepository` so that settlement runs in a proper unit-of-work and database context.

---

## Accounting Principles

### Zero-Sum Rule

- For the ledger to be consistent, **sum of all signed Journal Entry amounts must be zero**: Debits (positive) and Credits (negative) net out across all accounts.
- Tests enforce this after complex flows (e.g. `DoubleEntry_Balance_AlwaysZero`, stress tests) by querying `JournalEntries` and asserting `Sum(Debit ? Amount : -Amount) == 0.00m`.

### Journal Entries and Financial Integrity

- Every trade is stored as a **Transaction** with two entries: buyer account Debit, seller account Credit (same amount). No single-sided updates.
- Settlement-phase transactions reference clearing transaction IDs and keep an audit trail from clearing to settlement.
- Domain exceptions (`InsufficientFundsException`, `InvalidOutcomeException`, `TransactionNotBalancedException`) protect invariants and are used by both API and tests.

---

## Testing Strategy

The project maintains a **45-test** suite (unit, integration, stress, and security) that runs against a **SQLite in-memory** database for speed and CI compatibility. No live PostgreSQL is required for tests.

### Test Categories

- **Unit**: `OrderBookTests`, `OrderTests`, `LedgerTests`, `CopyTradingServiceTests` — domain and services in isolation.
- **Integration**: `MarketIntegrationTests`, `SocialTradingTests`, `DrakeFlowTests`, `GrandFinalIntegrationTests` — full stacks (MarketService, LedgerService, CopyTrading, Oracle) with shared in-memory DB.
- **Database / Integrity**: `DatabaseIntegrityTests` — rollback on failure (ThrowingTransactionRepository), order mapping fidelity, concurrent withdrawals, and double-entry zero-sum.
- **Performance / Stress**: `PerformanceStressTests` — high-volume match-making (1 000 buy + 1 000 sell orders via `Task.WhenAll`) and concurrent copy-trading (500 followers bombarding the ledger); both assert zero-sum afterward.
- **Security / Negative**: `SecurityIntegrityTests` — insufficient funds rejection, invalid outcome rejection (`DomainException`), and negative price rejection (Order constructor); no ledger pollution.

### Setup

- **EnterpriseTestSetup** provides `CreateFreshDbContext()` (SQLite in-memory, connection held open), `CreateServiceProvider(context)`, and stack helpers (`CreateMarketStack`, `CreateFullStackWithContext`, etc.) so all tests share the same persistence and service patterns.

### CI

- GitHub Actions (`.github/workflows/dotnet-ci.yml`) runs on every push and pull request to `main`: restore, build, and test the solution. No external database is required in CI.

---

## Getting Started

### Prerequisites

- .NET 8 SDK  
- Docker and Docker Compose (for running the API with PostgreSQL)

### Run Tests

```bash
# Restore, build, and run all tests
dotnet test

# Run only tests in a given class
dotnet test --filter "FullyQualifiedName~SecurityIntegrityTests"

# Run with verbose output
dotnet test --verbosity normal
```

All tests use SQLite in-memory and complete without a real Postgres instance.

### Run the API with Docker

```bash
# Build and run API + PostgreSQL
docker-compose up --build

# API is available at http://localhost:5050 (mapped from container port 8080)
# PostgreSQL: localhost:5432, database=projectexchange, user=postgres, password=postgres
```

Migrations are applied on startup via `db.Database.Migrate()` in `Program.cs`.

### Run the API Locally (without Docker)

1. Ensure PostgreSQL is running and create a database (or use the same connection string as in `appsettings.json`).
2. From the solution root:

```bash
dotnet run --project ProjectExchange.Core
```

Use the connection string in `appsettings.json` or override with environment variables.

---

## Summary

Project Exchange is built for **robustness**: clearing and settlement are split, the ledger is strictly double-entry and zero-sum, and every trade is validated (balance, outcome registration, price). The 45-test suite (including high-volume stress and security/integrity tests) and CI pipeline give developers confidence to extend the system without breaking financial invariants.
