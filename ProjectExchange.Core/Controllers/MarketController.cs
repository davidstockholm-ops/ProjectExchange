using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using ProjectExchange.Accounting.Domain.Abstractions;
using ProjectExchange.Accounting.Domain.Entities;
using ProjectExchange.Accounting.Domain.Enums;
using ProjectExchange.Accounting.Domain.Services;
using ProjectExchange.Core.Celebrity;
using ProjectExchange.Core.Markets;

namespace ProjectExchange.Core.Controllers;

/// <summary>Unified market API: Base, Flash, and Celebrity creation; active events; order book; outcome-reached (universal settlement).</summary>
[ApiController]
[Route("api/markets")]
[Produces("application/json")]
public class MarketController : ControllerBase
{
    private readonly IOutcomeOracle _oracle;
    private readonly FlashOracleService _flashOracle;
    private readonly MarketService _marketService;
    private readonly CopyTradingEngine _copyTradingEngine;
    private readonly IAccountRepository _accountRepository;
    private readonly LedgerService _ledgerService;
    private readonly IWebHostEnvironment _env;

    public MarketController(
        IOutcomeOracle oracle,
        FlashOracleService flashOracle,
        MarketService marketService,
        CopyTradingEngine copyTradingEngine,
        IAccountRepository accountRepository,
        LedgerService ledgerService,
        IWebHostEnvironment env)
    {
        _oracle = oracle ?? throw new ArgumentNullException(nameof(oracle));
        _flashOracle = flashOracle ?? throw new ArgumentNullException(nameof(flashOracle));
        _marketService = marketService ?? throw new ArgumentNullException(nameof(marketService));
        _copyTradingEngine = copyTradingEngine ?? throw new ArgumentNullException(nameof(copyTradingEngine));
        _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
        _ledgerService = ledgerService ?? throw new ArgumentNullException(nameof(ledgerService));
        _env = env ?? throw new ArgumentNullException(nameof(env));
    }

    // ----- Base -----

    /// <summary>Create a generic Base-type market (not Flash or Celebrity). Event stored, OrderBook created, MarketOpened raised.</summary>
    [HttpPost("base/create")]
    [ProducesResponseType(typeof(CreateBaseMarketResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult CreateBase([FromBody] CreateBaseMarketRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Title))
            return BadRequest("Title is required.");
        if (request.DurationMinutes < 1)
            return BadRequest("DurationMinutes must be at least 1.");

        var evt = _oracle.CreateMarketEvent(request.Title.Trim(), MarketEvent.EventType.Base, request.DurationMinutes, context: null);

        return Ok(new CreateBaseMarketResponse(
            evt.Id,
            evt.Title,
            evt.Type,
            evt.OutcomeId,
            evt.ActorId,
            evt.ResponsibleOracleId,
            evt.DurationMinutes,
            evt.CreatedAt,
            evt.ExpiresAt));
    }

    // ----- Flash -----

    /// <summary>Create a Flash market. Event type is always Flash; duration capped at 15 minutes. Event stored, OrderBook created, MarketOpened raised.</summary>
    [HttpPost("flash/create")]
    [ProducesResponseType(typeof(CreateFlashMarketResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreateFlashMarketResponse>> CreateFlash([FromBody] CreateFlashMarketRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Title))
            return BadRequest("Title is required.");
        if (request.DurationMinutes < 1)
            return BadRequest("DurationMinutes must be at least 1.");

        var evt = await _flashOracle.CreateFlashMarketAsync(request.Title.Trim(), request.DurationMinutes);

        return Ok(new CreateFlashMarketResponse(
            evt.Id,
            evt.Title,
            evt.Type,
            evt.OutcomeId,
            evt.ResponsibleOracleId,
            evt.DurationMinutes,
            evt.CreatedAt,
            evt.ExpiresAt));
    }

    // ----- Celebrity -----

    /// <summary>Create a Celebrity-type market for the given actor. Event stored, OrderBook created, MarketOpened raised. Celebrity-specific.</summary>
    [HttpPost("celebrity/create")]
    [ProducesResponseType(typeof(CreateCelebrityMarketResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult CreateCelebrity([FromBody] CreateCelebrityMarketRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Title))
            return BadRequest("Title is required.");
        if (request.DurationMinutes < 1)
            return BadRequest("DurationMinutes must be at least 1.");

        var actorId = string.IsNullOrWhiteSpace(request.ActorId) ? "Unknown" : request.ActorId.Trim();
        var evt = _oracle.CreateMarketEvent(actorId, request.Title.Trim(), request.Type?.Trim() ?? MarketEvent.EventType.Base, request.DurationMinutes);

        return Ok(new CreateCelebrityMarketResponse(
            evt.Id,
            evt.Title,
            evt.Type,
            evt.OutcomeId,
            evt.ActorId,
            evt.ResponsibleOracleId,
            evt.DurationMinutes,
            evt.CreatedAt,
            evt.ExpiresAt));
    }

    /// <summary>Simulate a celebrity trade (copy-trading). Celebrity-specific: wallets auto-created if missing. Use outcome-reached after event resolves to settle.</summary>
    [HttpPost("celebrity/simulate")]
    [ProducesResponseType(typeof(SimulateTradeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SimulateCelebrity([FromBody] SimulateTradeRequest body, CancellationToken cancellationToken = default)
    {
        if (body == null)
            return BadRequest("Request body is required.");
        if (body.Amount <= 0)
            return BadRequest("Amount must be positive.");
        if (string.IsNullOrWhiteSpace(body.OutcomeId))
            return BadRequest("OutcomeId is required.");

        var operatorId = string.IsNullOrWhiteSpace(body.OperatorId) ? CelebrityConstants.MasterTraderId : body.OperatorId.Trim();
        var actorId = body.ActorId?.Trim();
        var actorWalletName = CelebrityConstants.GetMainOperatingAccountName(actorId);

        await EnsureActorWalletExistsAsync(operatorId, actorId, actorWalletName, cancellationToken);

        var signal = _oracle.SimulateTrade(
            operatorId,
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

    // ----- Outcome Reached (universal) -----

    /// <summary>Mark outcome as reached and trigger auto-settlement (universal via IMarketOracle). Idempotent. Optional ConfidenceScore and SourceVerificationList for Agentic AI.</summary>
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
        var result = await _oracle.NotifyOutcomeReachedAsync(
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

    // ----- List & Order Book -----

    /// <summary>Returns all currently tradeable (active, non-expired) markets from all oracles (Base, Flash, Celebrity).</summary>
    [HttpGet("active")]
    [ProducesResponseType(typeof(IEnumerable<ActiveMarketResponse>), StatusCodes.Status200OK)]
    public IActionResult GetActive()
    {
        var fromCelebrity = _oracle.GetActiveEvents();
        var fromFlash = _flashOracle.GetActiveEvents();
        var events = fromCelebrity.Concat(fromFlash);
        var response = events.Select(e => new ActiveMarketResponse(
            e.Id,
            e.Title,
            e.Type,
            e.OutcomeId,
            e.ActorId,
            e.ResponsibleOracleId,
            e.DurationMinutes,
            e.CreatedAt,
            e.ExpiresAt));
        return Ok(response);
    }

    /// <summary>Order book for the given outcome (by OutcomeId). Bids descending by price, asks ascending by price.</summary>
    [HttpGet("orderbook/{outcomeId}")]
    [ProducesResponseType(typeof(OrderBookResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult GetOrderBook(string outcomeId)
    {
        if (string.IsNullOrWhiteSpace(outcomeId))
            return BadRequest("OutcomeId is required.");
        var book = _marketService.GetOrderBook(outcomeId.Trim());
        if (book == null)
            return Ok(new OrderBookResponse(outcomeId.Trim(), Array.Empty<OrderBookLevel>(), Array.Empty<OrderBookLevel>()));
        var bids = book.Bids.Select(o => new OrderBookLevel(o.Id, o.UserId, o.Price, o.Quantity)).ToList();
        var asks = book.Asks.Select(o => new OrderBookLevel(o.Id, o.UserId, o.Price, o.Quantity)).ToList();
        return Ok(new OrderBookResponse(outcomeId.Trim(), bids, asks));
    }

    // ----- Helpers (celebrity simulate) -----

    private async Task EnsureActorWalletExistsAsync(string operatorId, string? actorId, string actorWalletName, CancellationToken cancellationToken)
    {
        var accounts = await _accountRepository.GetByOperatorIdAsync(operatorId, cancellationToken);
        if (accounts.Any(a => string.Equals(a.Name, actorWalletName, StringComparison.Ordinal)))
            return;

        var celebrityAccountId = Guid.NewGuid();
        var celebrityAccount = new Account(celebrityAccountId, actorWalletName, AccountType.Asset, operatorId);
        await _accountRepository.CreateAsync(celebrityAccount, cancellationToken);

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
}

// ----- Request/Response DTOs -----

public record CreateBaseMarketRequest(string? Title, int DurationMinutes);
public record CreateBaseMarketResponse(Guid Id, string Title, string Type, string OutcomeId, string ActorId, string ResponsibleOracleId, int DurationMinutes, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt);

public record CreateFlashMarketRequest(string? Title, int DurationMinutes);
public record CreateFlashMarketResponse(Guid Id, string Title, string Type, string OutcomeId, string ResponsibleOracleId, int DurationMinutes, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt);

public record CreateCelebrityMarketRequest(string? ActorId, string? Title, string? Type, int DurationMinutes);
public record CreateCelebrityMarketResponse(Guid Id, string Title, string Type, string OutcomeId, string ActorId, string ResponsibleOracleId, int DurationMinutes, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt);

public record SimulateTradeRequest(string? OperatorId, decimal Amount, string OutcomeId, string? OutcomeName = null, string? ActorId = null);
public record SimulateTradeResponse(Guid TradeId, string OperatorId, decimal Amount, string OutcomeId, string OutcomeName, string? ActorId, Guid? ClearingTransactionId, string Phase);

public record OutcomeReachedRequest(string? OutcomeId, decimal? ConfidenceScore = null, IReadOnlyList<string>? SourceVerificationList = null);
public record OutcomeReachedResponse(string OutcomeId, IReadOnlyList<Guid> SettlementTransactionIds, IReadOnlyList<Guid> AlreadySettledClearingIds, string Message, decimal? ConfidenceScore = null, IReadOnlyList<string>? SourceVerificationList = null);

public record ActiveMarketResponse(Guid Id, string Title, string Type, string OutcomeId, string ActorId, string ResponsibleOracleId, int DurationMinutes, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt);
public record OrderBookResponse(string OutcomeId, IReadOnlyList<OrderBookLevel> Bids, IReadOnlyList<OrderBookLevel> Asks);
public record OrderBookLevel(Guid OrderId, string UserId, decimal Price, decimal Quantity);
