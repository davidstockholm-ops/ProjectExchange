using Microsoft.AspNetCore.Mvc;
using ProjectExchange.Core.Drake;

namespace ProjectExchange.Core.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DrakeController : ControllerBase
{
    private readonly DrakeOracleService _oracle;
    private readonly CopyTradingEngine _copyTradingEngine;
    private readonly AutoSettlementAgent _autoSettlementAgent;

    public DrakeController(
        DrakeOracleService oracle,
        CopyTradingEngine copyTradingEngine,
        AutoSettlementAgent autoSettlementAgent)
    {
        _oracle = oracle ?? throw new ArgumentNullException(nameof(oracle));
        _copyTradingEngine = copyTradingEngine ?? throw new ArgumentNullException(nameof(copyTradingEngine));
        _autoSettlementAgent = autoSettlementAgent ?? throw new ArgumentNullException(nameof(autoSettlementAgent));
    }

    /// <summary>
    /// Triggers Drake oracle to simulate a celebrity trade (e.g. Drake betting on an outcome).
    /// CopyTradingEngine subscribes and posts Clearing-phase debit (Drake Main Operating Account)
    /// and credit (Market Holding Account for that outcome). All transactions use Clearing for high-frequency traffic.
    /// </summary>
    [HttpPost("simulate")]
    public async Task<IActionResult> Simulate([FromBody] SimulateTradeRequest request)
    {
        if (request == null)
            return BadRequest("Request body is required.");
        if (request.Amount <= 0)
            return BadRequest("Amount must be positive.");
        if (string.IsNullOrWhiteSpace(request.OutcomeId))
            return BadRequest("OutcomeId is required.");

        var operatorId = request.OperatorId != Guid.Empty ? request.OperatorId : DrakeConstants.MasterTraderId;
        var signal = _oracle.SimulateTrade(
            operatorId,
            request.Amount,
            request.OutcomeId.Trim(),
            request.OutcomeName?.Trim() ?? "Outcome X");

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
            clearingTransactionId,
            "Clearing"));
    }

    /// <summary>
    /// Marks outcome as reached and triggers the Auto-Settlement Agent to handle Drake trade outcomes:
    /// posts Settlement-phase transactions for each clearing transaction tied to this outcome. Idempotent.
    /// </summary>
    [HttpPost("outcome-reached")]
    public async Task<ActionResult<OutcomeReachedResponse>> OutcomeReached(
        [FromBody] OutcomeReachedRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.OutcomeId))
            return BadRequest("OutcomeId is required.");

        var outcomeId = request.OutcomeId!.Trim();
        var result = await _autoSettlementAgent.SettleOutcomeAsync(
            outcomeId,
            cancellationToken);

        return Ok(new OutcomeReachedResponse(
            result.OutcomeId,
            result.NewSettlementTransactionIds,
            result.AlreadySettledClearingIds,
            result.Message));
    }
}

public record SimulateTradeRequest(Guid OperatorId, decimal Amount, string OutcomeId, string? OutcomeName = null);
public record SimulateTradeResponse(
    Guid TradeId,
    Guid OperatorId,
    decimal Amount,
    string OutcomeId,
    string OutcomeName,
    Guid? ClearingTransactionId,
    string Phase);

public record OutcomeReachedRequest(string? OutcomeId);
public record OutcomeReachedResponse(
    string OutcomeId,
    IReadOnlyList<Guid> SettlementTransactionIds,
    IReadOnlyList<Guid> AlreadySettledClearingIds,
    string Message);
