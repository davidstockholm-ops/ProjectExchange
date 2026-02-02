using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using ProjectExchange.Accounting.Domain.Abstractions;
using ProjectExchange.Accounting.Domain.Entities;
using ProjectExchange.Accounting.Domain.Enums;
using ProjectExchange.Accounting.Domain.Services;

namespace ProjectExchange.Core.Drake;

/// <summary>
/// Auto-Settlement Agent: handles the outcome of Drake trades. When an outcome is reached,
/// automatically settles the related clearing transaction(s) by posting Settlement-phase
/// transactions (reverse entries) that reference each clearing tx. Idempotent: already-settled
/// clearing transactions are skipped.
/// </summary>
public class AutoSettlementAgent
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly CopyTradingEngine _copyTradingEngine;
    /// <summary>Clearing transaction IDs that have already been settled (idempotency).</summary>
    private readonly ConcurrentDictionary<Guid, Guid> _settledClearingToSettlementTx = new();

    public AutoSettlementAgent(IServiceScopeFactory scopeFactory, CopyTradingEngine copyTradingEngine)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _copyTradingEngine = copyTradingEngine ?? throw new ArgumentNullException(nameof(copyTradingEngine));
    }

    /// <summary>
    /// Handles the outcome for celebrity trades: finds all clearing transaction(s) for this outcome,
    /// posts Settlement-phase transactions (reverse entries) for any not yet settled. Idempotent.
    /// Supports Agentic AI reporting: optional confidence score and source verification list.
    /// </summary>
    /// <param name="outcomeId">Outcome that was reached (e.g. "outcome-x").</param>
    /// <param name="confidenceScore">Optional confidence score from the oracle/agent (e.g. 0.0–1.0).</param>
    /// <param name="sourceVerificationList">Optional list of sources the agent used to verify the outcome.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result with newly settled transaction IDs, any already-settled clearing IDs, and optional confidence/sources.</returns>
    public async Task<SettlementResult> SettleOutcomeAsync(
        string outcomeId,
        decimal? confidenceScore = null,
        IReadOnlyList<string>? sourceVerificationList = null,
        CancellationToken cancellationToken = default)
    {
        var clearingTxIds = _copyTradingEngine.GetClearingTransactionIdsForOutcome(outcomeId);
        if (clearingTxIds.Count == 0)
            return new SettlementResult(outcomeId, NewSettlementTransactionIds: [], AlreadySettledClearingIds: [], Message: "No clearing transactions found for this outcome. Call POST /api/celebrity/simulate with the same outcomeId first (matching is case-insensitive). If the app restarted, simulate again before outcome-reached.", confidenceScore, sourceVerificationList ?? Array.Empty<string>());

        await using var scope = _scopeFactory.CreateAsyncScope();
        var ledgerService = scope.ServiceProvider.GetRequiredService<LedgerService>();
        var transactionRepository = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();

        var newSettlementIds = new List<Guid>();
        var alreadySettledClearingIds = new List<Guid>();

        foreach (var clearingTxId in clearingTxIds)
        {
            if (_settledClearingToSettlementTx.TryGetValue(clearingTxId, out var existingSettlementId))
            {
                alreadySettledClearingIds.Add(clearingTxId);
                newSettlementIds.Add(existingSettlementId); // include in response for consistency
                continue;
            }

            var clearingTx = await transactionRepository.GetByIdAsync(clearingTxId, cancellationToken);
            if (clearingTx == null)
                continue;

            // Reverse each entry with Settlement phase (Debit -> Credit, Credit -> Debit, same amounts)
            var settlementEntries = clearingTx.JournalEntries
                .Select(e => new JournalEntry(
                    e.AccountId,
                    e.Amount,
                    e.EntryType == EntryType.Debit ? EntryType.Credit : EntryType.Debit,
                    SettlementPhase.Settlement))
                .ToList();

            var settlementTxId = await ledgerService.PostTransactionAsync(
                settlementEntries,
                settlesClearingTransactionId: clearingTxId,
                type: null,
                cancellationToken);

            _settledClearingToSettlementTx.TryAdd(clearingTxId, settlementTxId);
            newSettlementIds.Add(settlementTxId);
        }

        var message = alreadySettledClearingIds.Count > 0
            ? $"Settled {newSettlementIds.Count - alreadySettledClearingIds.Count} new; {alreadySettledClearingIds.Count} already settled."
            : "Auto-Settlement completed for all clearing transactions.";

        return new SettlementResult(
            outcomeId,
            newSettlementIds,
            alreadySettledClearingIds,
            message,
            confidenceScore,
            sourceVerificationList ?? Array.Empty<string>());
    }

    /// <summary>Whether the given clearing transaction has already been settled.</summary>
    public bool IsSettled(Guid clearingTransactionId) => _settledClearingToSettlementTx.ContainsKey(clearingTransactionId);
}

/// <summary>Result of handling an outcome via the Auto-Settlement Agent. Includes optional confidence and sources for Agentic AI.</summary>
public record SettlementResult(
    string OutcomeId,
    IReadOnlyList<Guid> NewSettlementTransactionIds,
    IReadOnlyList<Guid> AlreadySettledClearingIds,
    string Message,
    decimal? ConfidenceScore = null,
    IReadOnlyList<string>? SourceVerificationList = null);
