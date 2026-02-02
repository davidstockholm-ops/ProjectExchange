using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using ProjectExchange.Accounting.Domain.Abstractions;
using ProjectExchange.Accounting.Domain.Entities;
using ProjectExchange.Accounting.Domain.Enums;
using ProjectExchange.Accounting.Domain.Services;
using ProjectExchange.Core.Markets;

namespace ProjectExchange.Core.Drake;

/// <summary>
/// Subscribes to IOutcomeOracle (e.g. CelebrityOracleService) and uses LedgerService to execute copy-trades:
/// debit the celebrity Main Operating Account (per actor), credit a Market Holding Account per outcome. All entries Clearing.
/// </summary>
public class CopyTradingEngine
{
    private readonly IServiceScopeFactory _scopeFactory;
    /// <summary>Key = outcomeId (case-insensitive). Clearing tx IDs stored when simulate runs. Access under same lock as _outcomeClearingListsLock.</summary>
    private readonly ConcurrentDictionary<string, List<Guid>> _outcomeToClearingTransactionIds = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Outcome-specific Market Holding accounts (one per outcome). Key = outcomeId (case-insensitive).</summary>
    private readonly ConcurrentDictionary<string, Guid> _outcomeToMarketHoldingAccountId = new(StringComparer.OrdinalIgnoreCase);

    public CopyTradingEngine(IServiceScopeFactory scopeFactory, IOutcomeOracle oracle)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        if (oracle == null) throw new ArgumentNullException(nameof(oracle));

        oracle.TradeProposed += OnTradeProposed;
    }

    /// <summary>Clearing transaction IDs for an outcome (for Auto-Settlement). Thread-safe.</summary>
    public IReadOnlyList<Guid> GetClearingTransactionIdsForOutcome(string outcomeId)
    {
        var key = outcomeId ?? string.Empty;
        var lockObj = _outcomeClearingListsLock.GetOrAdd(key, _ => new object());
        lock (lockObj)
        {
            return _outcomeToClearingTransactionIds.TryGetValue(key, out var list)
                ? list.ToList().AsReadOnly()
                : Array.Empty<Guid>();
        }
    }

    /// <summary>Last clearing transaction ID for an outcome (for API response after simulate). Thread-safe.</summary>
    public Guid? GetLastClearingTransactionIdForOutcome(string outcomeId)
    {
        var list = GetClearingTransactionIdsForOutcome(outcomeId);
        return list.Count > 0 ? list[^1] : null;
    }

    private void OnTradeProposed(object? sender, CelebrityTradeSignal signal)
    {
        try
        {
            ExecuteCopyTradeAsync(signal).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CopyTradingEngine] OnTradeProposed failed: {ex.Message}");
            Console.WriteLine($"[CopyTradingEngine] StackTrace: {ex.StackTrace}");
        }
    }

    /// <summary>
    /// Executes the copy-trade: debit the celebrity Main Operating Account (per actor), credit Market Holding Account for this outcome (Clearing).
    /// </summary>
    public async Task<Guid> ExecuteCopyTradeAsync(CelebrityTradeSignal signal, CancellationToken cancellationToken = default)
    {
        var mainAccountName = DrakeConstants.GetMainOperatingAccountName(signal.ActorId);
        Console.WriteLine($"[CopyTradingEngine] ExecuteCopyTradeAsync: OperatorId={signal.OperatorId}, ActorId={signal.ActorId}, OutcomeId={signal.OutcomeId}, Amount={signal.Amount}");

        await using var scope = _scopeFactory.CreateAsyncScope();
        var accountRepository = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
        var ledgerService = scope.ServiceProvider.GetRequiredService<LedgerService>();

        var celebrityAccounts = await accountRepository.GetByOperatorIdAsync(signal.OperatorId, cancellationToken);
        var accountNames = celebrityAccounts.Select(a => a.Name).ToList();
        Console.WriteLine($"[CopyTradingEngine] Accounts for operator {signal.OperatorId}: count={celebrityAccounts.Count}, names=[{string.Join(", ", accountNames)}]");

        var mainAccount = celebrityAccounts.FirstOrDefault(a => a.Name == mainAccountName);
        if (mainAccount == null)
        {
            Console.WriteLine($"[CopyTradingEngine] FAILED: Account '{mainAccountName}' NOT FOUND for operator {signal.OperatorId}. Create the wallet first (POST /api/wallet/create with Name=\"{mainAccountName}\").");
            throw new InvalidOperationException(
                $"Account '{mainAccountName}' not found for operator {signal.OperatorId}. Create the wallet first.");
        }
        Console.WriteLine($"[CopyTradingEngine] Found Main Operating Account: Id={mainAccount.Id}, Name={mainAccount.Name}");

        var marketAccount = await GetOrCreateMarketHoldingAccountForOutcomeAsync(
            signal.OutcomeId,
            signal.OutcomeName,
            accountRepository,
            cancellationToken);

        var entries = new List<JournalEntry>
        {
            new(mainAccount.Id, signal.Amount, EntryType.Debit, SettlementPhase.Clearing),
            new(marketAccount.Id, signal.Amount, EntryType.Credit, SettlementPhase.Clearing)
        };

        var clearingTransactionId = await ledgerService.PostTransactionAsync(
            entries,
            settlesClearingTransactionId: null,
            type: null,
            cancellationToken);

        Console.WriteLine($"[CopyTradingEngine] Clearing transaction created: Id={clearingTransactionId}, OutcomeId={signal.OutcomeId}");

        var key = signal.OutcomeId;
        var lockObj = _outcomeClearingListsLock.GetOrAdd(key, _ => new object());
        lock (lockObj)
        {
            _outcomeToClearingTransactionIds.AddOrUpdate(
                key,
                _ => new List<Guid> { clearingTransactionId },
                (_, list) => { list.Add(clearingTransactionId); return list; });
        }

        Console.WriteLine($"[CopyTradingEngine] ExecuteCopyTradeAsync completed successfully for OutcomeId={signal.OutcomeId}");
        return clearingTransactionId;
    }

    /// <summary>Get or create a Market Holding Account representing the specific outcome (Clearing & Settlement).</summary>
    private async Task<Account> GetOrCreateMarketHoldingAccountForOutcomeAsync(string outcomeId, string outcomeName, IAccountRepository accountRepository, CancellationToken cancellationToken)
    {
        var semaphore = _outcomeSemaphores.GetOrAdd(outcomeId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            if (_outcomeToMarketHoldingAccountId.TryGetValue(outcomeId, out var existingId))
            {
                var existing = await accountRepository.GetByIdAsync(existingId, cancellationToken);
                if (existing != null)
                    return existing;
            }

            var id = Guid.NewGuid();
            var name = $"{DrakeConstants.MarketHoldingAccountNamePrefix}{outcomeName?.Trim() ?? outcomeId}";
            var account = new Account(id, name, AccountType.Liability, DrakeConstants.SystemOperatorId);
            await accountRepository.CreateAsync(account, cancellationToken);
            _outcomeToMarketHoldingAccountId.TryAdd(outcomeId, id);
            return account;
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>Per-outcome lock for clearing tx list (thread-safe add/read with defined order).</summary>
    private readonly ConcurrentDictionary<string, object> _outcomeClearingListsLock = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Per-outcome semaphore for Market Holding account get-or-create.</summary>
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _outcomeSemaphores = new(StringComparer.OrdinalIgnoreCase);
}
