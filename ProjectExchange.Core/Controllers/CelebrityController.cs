using Microsoft.AspNetCore.Mvc;
using ProjectExchange.Core.Drake;
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

    public CelebrityController(
        IOutcomeOracle oracle,
        CopyTradingEngine copyTradingEngine,
        AutoSettlementAgent autoSettlementAgent)
    {
        _oracle = oracle ?? throw new ArgumentNullException(nameof(oracle));
        _copyTradingEngine = copyTradingEngine ?? throw new ArgumentNullException(nameof(copyTradingEngine));
        _autoSettlementAgent = autoSettlementAgent ?? throw new ArgumentNullException(nameof(autoSettlementAgent));
    }

    /// <summary>
    /// Simulate a celebrity trade (e.g. Drake or Elon betting on an outcome). Include ActorId in the body for multi-celebrity (e.g. "Elon").
    /// CopyTradingEngine posts Clearing-phase entries; use outcome-reached after the event resolves to settle.
    /// </summary>
    [HttpPost("simulate")]
    [ProducesResponseType(typeof(SimulateTradeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Simulate([FromBody] SimulateTradeRequest body)
    {
        if (body == null)
            return BadRequest("Request body is required.");
        if (body.Amount <= 0)
            return BadRequest("Amount must be positive.");
        if (string.IsNullOrWhiteSpace(body.OutcomeId))
            return BadRequest("OutcomeId is required.");

        var operatorId = body.OperatorId != Guid.Empty ? body.OperatorId : DrakeConstants.MasterTraderId;
        var signal = _oracle.SimulateTrade(
            operatorId,
            body.Amount,
            body.OutcomeId.Trim(),
            body.OutcomeName?.Trim() ?? "Outcome X",
            body.ActorId?.Trim());

        // Ensure state is stored in the same CopyTradingEngine instance used by outcome-reached (singleton)
        var clearingTransactionId = _copyTradingEngine.GetLastClearingTransactionIdForOutcome(signal.OutcomeId);
        if (clearingTransactionId == null)
        {
            // Event handler may not have run or may have thrown; run copy-trade directly so state is stored
            clearingTransactionId = await _copyTradingEngine.ExecuteCopyTradeAsync(signal);
        }

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

/// <summary>Request to simulate a celebrity trade. ActorId identifies the celebrity (e.g. Drake, Elon) for account naming.</summary>
/// <param name="OperatorId">Operator (celebrity) account ID; use default (empty) for Master Trader.</param>
/// <param name="Amount">Trade amount (must be positive).</param>
/// <param name="OutcomeId">Outcome identifier (e.g. from Active Events).</param>
/// <param name="OutcomeName">Display name for the outcome.</param>
/// <param name="ActorId">Optional celebrity/actor ID (e.g. "Drake", "Elon"). Used for Main Operating Account naming; omit for default "Drake".</param>
public record SimulateTradeRequest(Guid OperatorId, decimal Amount, string OutcomeId, string? OutcomeName = null, string? ActorId = null);

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
