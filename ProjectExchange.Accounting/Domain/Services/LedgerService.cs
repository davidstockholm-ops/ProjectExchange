using ProjectExchange.Accounting.Domain.Abstractions;
using ProjectExchange.Accounting.Domain.Entities;
using ProjectExchange.Accounting.Domain.Enums;

namespace ProjectExchange.Accounting.Domain.Services;

/// <summary>
/// Calculates current balances from the ledger. Supports Clearing & Settlement Split [2026-01-31]:
/// clearing balance = internal debt/obligation; settled balance = external settlement.
/// </summary>
public class LedgerService
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly IUnitOfWork _unitOfWork;

    public LedgerService(
        ITransactionRepository transactionRepository,
        IAccountRepository accountRepository,
        IUnitOfWork unitOfWork)
    {
        _transactionRepository = transactionRepository ?? throw new ArgumentNullException(nameof(transactionRepository));
        _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    /// <summary>
    /// Balance for a single account: Debits - Credits. Optional filter by settlement phase.
    /// </summary>
    public async Task<decimal> GetAccountBalanceAsync(
        Guid accountId,
        SettlementPhase? phase = null,
        CancellationToken cancellationToken = default)
    {
        var transactions = await _transactionRepository.GetByAccountIdAsync(accountId, cancellationToken);
        return SumEntriesForAccount(transactions, accountId, phase);
    }

    /// <summary>
    /// Balances for all accounts belonging to an operator. Key = AccountId, Value = Debits - Credits.
    /// Optional filter by settlement phase (e.g. only Clearing to see internal debt).
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, decimal>> GetOperatorBalancesAsync(
        string operatorId,
        SettlementPhase? phase = null,
        CancellationToken cancellationToken = default)
    {
        var accounts = await _accountRepository.GetByOperatorIdAsync(operatorId, cancellationToken);
        var transactions = await _transactionRepository.GetByOperatorIdAsync(operatorId, cancellationToken);

        var balances = new Dictionary<Guid, decimal>();
        foreach (var account in accounts)
            balances[account.Id] = SumEntriesForAccount(transactions, account.Id, phase);

        return balances;
    }

    /// <summary>
    /// Single aggregate balance for an operator (sum of all account balances for that operator).
    /// Useful for "net position" in a given phase (Clearing vs Settlement).
    /// </summary>
    public async Task<decimal> GetOperatorNetBalanceAsync(
        string operatorId,
        SettlementPhase? phase = null,
        CancellationToken cancellationToken = default)
    {
        var byAccount = await GetOperatorBalancesAsync(operatorId, phase, cancellationToken);
        return byAccount.Values.Sum();
    }

    /// <summary>
    /// Posts a balanced transaction to the ledger. Used by CopyTradingEngine, Auto-Settlement, and MarketService.
    /// All entries must be balanced; phase is per-entry (Clearing or Settlement).
    /// </summary>
    public async Task<Guid> PostTransactionAsync(
        IReadOnlyList<JournalEntry> journalEntries,
        Guid? settlesClearingTransactionId = null,
        TransactionType? type = null,
        CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        var transaction = new Transaction(id, journalEntries.ToList(), null, settlesClearingTransactionId, type);
        await _transactionRepository.AppendAsync(transaction, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return id;
    }

    private static decimal SumEntriesForAccount(
        IReadOnlyList<Transaction> transactions,
        Guid accountId,
        SettlementPhase? phase)
    {
        decimal balance = 0;
        foreach (var tx in transactions)
        {
            foreach (var entry in tx.JournalEntries)
            {
                if (entry.AccountId != accountId)
                    continue;
                if (phase.HasValue && entry.Phase != phase.Value)
                    continue;

                balance += entry.EntryType == EntryType.Debit ? entry.Amount : -entry.Amount;
            }
        }
        return balance;
    }
}
