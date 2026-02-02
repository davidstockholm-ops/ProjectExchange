using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using ProjectExchange.Accounting.Domain.Abstractions;
using ProjectExchange.Accounting.Domain.Entities;
using ProjectExchange.Accounting.Domain.Enums;
using ProjectExchange.Accounting.Domain.Services;
using ProjectExchange.Core.Celebrity;
using ProjectExchange.Core.Markets;

namespace ProjectExchange.Core.Controllers;

/// <summary>Celebrity copy-trading and outcome settlement: simulate a celebrity trade, then mark outcome reached for auto-settlement.</summary>
[ApiController]
[Route("api/celebrity")]
public class CelebrityController : ControllerBase
{
    private readonly IOutcomeOracle _oracle;
    private readonly CopyTradingEngine _copyTradingEngine;
    private readonly AutoSettlementAgent _autoSettlementAgent;
    private readonly IAccountRepository _accountRepository;
    private readonly LedgerService _ledgerService;
    private readonly IWebHostEnvironment _env;

    public CelebrityController(
        IOutcomeOracle oracle,
        CopyTradingEngine copyTradingEngine,
        AutoSettlementAgent autoSettlementAgent,
        IAccountRepository accountRepository,
        LedgerService ledgerService,
        IWebHostEnvironment env)
    {
        _oracle = oracle ?? throw new ArgumentNullException(nameof(oracle));
        _copyTradingEngine = copyTradingEngine ?? throw new ArgumentNullException(nameof(copyTradingEngine));
        _autoSettlementAgent = autoSettlementAgent ?? throw new ArgumentNullException(nameof(autoSettlementAgent));
        _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
        _ledgerService = ledgerService ?? throw new ArgumentNullException(nameof(ledgerService));
        _env = env ?? throw new ArgumentNullException(nameof(env));
    }

    /// <summary>
    /// Simulate a celebrity trade (e.g. Drake or Elon betting on an outcome). OperatorId can be any string (e.g. "elon", "drake"); wallets are auto-created with default balance if missing.
    /// Actor's wallet is always named "{ActorId} Main Operating Account". Use outcome-reached after the event resolves to settle.
    /// </summary>
    [HttpPost("simulate")]
    [ProducesResponseType(typeof(SimulateTradeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Simulate([FromBody] SimulateTradeRequest body, CancellationToken cancellationToken = default)
    {
        if (body == null)
            return BadRequest("Request body is required.");
        if (body.Amount <= 0)
            return BadRequest("Amount must be positive.");
        if (string.IsNullOrWhiteSpace(body.OutcomeId))
            return BadRequest("OutcomeId is required.");

        var operatorGuid = ResolveOperatorId(body.OperatorId);
        var actorId = body.ActorId?.Trim();
        var actorWalletName = CelebrityConstants.GetMainOperatingAccountName(actorId);

        await EnsureActorWalletExistsAsync(operatorGuid, actorId, actorWalletName, cancellationToken);

        var signal = _oracle.SimulateTrade(
            operatorGuid,
            body.Amount,
            body.OutcomeId.Trim(),
            body.OutcomeName?.Trim() ?? "Outcome X",
            actorId);

        var clearingTransactionId = _copyTradingEngine.GetLastClearingTransactionIdForOutcome(signal.OutcomeId);
        if (clearingTransactionId == null)
            clearingTransactionId = await _copyTradingEngine.ExecuteCopyTradeAsync(signal, cancellationToken);

        return Ok(new SimulateTradeResponse(
            signal.TradeId,
            signal.OperatorId,
            signal.Amount,
            signal.OutcomeId,
            signal.OutcomeName,
            signal.ActorId,
            clearingTransactionId,
            "Clearing"));
    }

    /// <summary>Resolve OperatorId string to Guid: empty/null -> MasterTraderId; valid Guid -> parse; else deterministic Guid from string (SHA256 hash).</summary>
    private static Guid ResolveOperatorId(string? operatorId)
    {
        if (string.IsNullOrWhiteSpace(operatorId))
            return CelebrityConstants.MasterTraderId;
        if (Guid.TryParse(operatorId.Trim(), out var parsed))
            return parsed;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(operatorId.Trim()));
        var bytes = new byte[16];
        Array.Copy(hash, 0, bytes, 0, 16);
        return new Guid(bytes);
    }

    /// <summary>Ensure the actor's Main Operating Account exists for the operator; if not, create it. In Development only, also fund with default balance.</summary>
    private async Task EnsureActorWalletExistsAsync(Guid operatorGuid, string? actorId, string actorWalletName, CancellationToken cancellationToken)
    {
        var accounts = await _accountRepository.GetByOperatorIdAsync(operatorGuid, cancellationToken);
        if (accounts.Any(a => string.Equals(a.Name, actorWalletName, StringComparison.Ordinal)))
            return;

        var celebrityAccountId = Guid.NewGuid();
        var celebrityAccount = new Account(celebrityAccountId, actorWalletName, AccountType.Asset, operatorGuid);
        await _accountRepository.CreateAsync(celebrityAccount, cancellationToken);

        // TODO: This auto-funding is for DEVELOPMENT/TESTING purposes only. In production, liquidity must be provided via actual deposits or operator collateral.
        // Conditional: only credit new celebrity/operator accounts from Platform Treasury when running in Development.
        if (_env.IsDevelopment())
        {
            var platformTreasury = await GetOrCreatePlatformTreasuryAsync(cancellationToken);
            await _ledgerService.PostTransactionAsync(
                new List<JournalEntry>
                {
                    new(platformTreasury.Id, CelebrityConstants.DefaultAutoCreateBalance, EntryType.Debit, SettlementPhase.Clearing),
                    new(celebrityAccountId, CelebrityConstants.DefaultAutoCreateBalance, EntryType.Credit, SettlementPhase.Clearing)
                },
                settlesClearingTransactionId: null,
                type: null,
                cancellationToken);
        }
    }

    private async Task<Account> GetOrCreatePlatformTreasuryAsync(CancellationToken cancellationToken)
    {
        var existing = await _accountRepository.GetByIdAsync(CelebrityConstants.PlatformTreasuryAccountId, cancellationToken);
        if (existing != null)
            return existing;
        var account = new Account(CelebrityConstants.PlatformTreasuryAccountId, CelebrityConstants.PlatformTreasuryAccountName, AccountType.Asset, CelebrityConstants.SystemOperatorId);
        await _accountRepository.CreateAsync(account, cancellationToken);
        return account;
    }

    /// <summary>
    /// Mark outcome as reached and trigger auto-settlement. Idempotent. Optional ConfidenceScore (0–1) and SourceVerificationList for Agentic AI.
    /// </summary>
    [HttpPost("outcome-reached")]
    [ProducesResponseType(typeof(OutcomeReachedResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OutcomeReachedResponse>> OutcomeReached(
        [FromBody] OutcomeReachedRequest body,
        CancellationToken cancellationToken = default)
    {
        if (body == null || string.IsNullOrWhiteSpace(body.OutcomeId))
            return BadRequest("OutcomeId is required.");

        var outcomeId = body.OutcomeId!.Trim();
        var result = await _autoSettlementAgent.SettleOutcomeAsync(
            outcomeId,
            body.ConfidenceScore,
            body.SourceVerificationList,
            cancellationToken);

        return Ok(new OutcomeReachedResponse(
            result.OutcomeId,
            result.NewSettlementTransactionIds,
            result.AlreadySettledClearingIds,
            result.Message,
            result.ConfidenceScore,
            result.SourceVerificationList));
    }
}

/// <summary>Request to simulate a celebrity trade. OperatorId can be any string (e.g. "elon", "drake"); wallets are auto-created if missing.</summary>
/// <param name="OperatorId">Operator (celebrity) ID: any string (e.g. "elon") or a Guid. Empty/null uses Master Trader. Same string always maps to same internal operator.</param>
/// <param name="Amount">Trade amount (must be positive).</param>
/// <param name="OutcomeId">Outcome identifier (e.g. from Active Events).</param>
/// <param name="OutcomeName">Display name for the outcome.</param>
/// <param name="ActorId">Optional celebrity/actor ID (e.g. "Drake", "Elon"). Wallet is always named "{ActorId} Main Operating Account"; omit for default "Drake".</param>
public record SimulateTradeRequest(string? OperatorId, decimal Amount, string OutcomeId, string? OutcomeName = null, string? ActorId = null);

public record SimulateTradeResponse(
    Guid TradeId,
    Guid OperatorId,
    decimal Amount,
    string OutcomeId,
    string OutcomeName,
    string? ActorId,
    Guid? ClearingTransactionId,
    string Phase);

/// <summary>Request to mark an outcome as reached and trigger auto-settlement. Optional ConfidenceScore and SourceVerificationList for Agentic AI reporting.</summary>
/// <param name="OutcomeId">Outcome that was reached (required).</param>
/// <param name="ConfidenceScore">Optional confidence score from the agent (e.g. 0.0–1.0).</param>
/// <param name="SourceVerificationList">Optional list of source URLs or identifiers the agent used to verify the outcome.</param>
public record OutcomeReachedRequest(string? OutcomeId, decimal? ConfidenceScore = null, IReadOnlyList<string>? SourceVerificationList = null);

public record OutcomeReachedResponse(
    string OutcomeId,
    IReadOnlyList<Guid> SettlementTransactionIds,
    IReadOnlyList<Guid> AlreadySettledClearingIds,
    string Message,
    decimal? ConfidenceScore = null,
    IReadOnlyList<string>? SourceVerificationList = null);
