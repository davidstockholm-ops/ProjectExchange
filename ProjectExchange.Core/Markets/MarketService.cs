using Microsoft.EntityFrameworkCore.Storage;
using ProjectExchange.Accounting.Domain.Abstractions;
using ProjectExchange.Accounting.Domain.Entities;
using ProjectExchange.Accounting.Domain.Enums;
using ProjectExchange.Accounting.Domain.Exceptions;
using ProjectExchange.Accounting.Domain.Services;
using ProjectExchange.Core.Infrastructure.Persistence;
using ProjectExchange.Core.Social;

namespace ProjectExchange.Core.Markets;

/// <summary>
/// Secondary market (liquid contracts): places orders, runs matching, and records each match in the ledger as a Trade.
/// Assets are identified by OutcomeId only (universal; not tied to celebrity or event type). Uses <see cref="IDbContextTransaction"/>
/// so ledger updates and order-book updates succeed or fail together (atomicity). Rejects orders for unregistered outcomes
/// (if IOutcomeRegistry provided) and insufficient buyer funds. After a successful placement, if the user is a Master
/// (has followers), mirrors the order for each follower via CopyTradingService.
/// </summary>
public class MarketService
{
    private readonly IOrderBookStore _orderBookStore;
    private readonly IAccountRepository _accountRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ProjectExchangeDbContext _dbContext;
    private readonly CopyTradingService _copyTradingService;
    private readonly LedgerService _ledgerService;
    private readonly AccountingService _accountingService;
    private readonly IOutcomeAssetTypeResolver _outcomeAssetTypeResolver;
    private readonly IOutcomeRegistry? _outcomeRegistry;

    public MarketService(
        IOrderBookStore orderBookStore,
        IAccountRepository accountRepository,
        ITransactionRepository transactionRepository,
        ProjectExchangeDbContext dbContext,
        CopyTradingService copyTradingService,
        LedgerService ledgerService,
        AccountingService accountingService,
        IOutcomeAssetTypeResolver outcomeAssetTypeResolver,
        IOutcomeRegistry? outcomeRegistry = null)
    {
        _orderBookStore = orderBookStore ?? throw new ArgumentNullException(nameof(orderBookStore));
        _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
        _transactionRepository = transactionRepository ?? throw new ArgumentNullException(nameof(transactionRepository));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _copyTradingService = copyTradingService ?? throw new ArgumentNullException(nameof(copyTradingService));
        _ledgerService = ledgerService ?? throw new ArgumentNullException(nameof(ledgerService));
        _accountingService = accountingService ?? throw new ArgumentNullException(nameof(accountingService));
        _outcomeAssetTypeResolver = outcomeAssetTypeResolver ?? throw new ArgumentNullException(nameof(outcomeAssetTypeResolver));
        _outcomeRegistry = outcomeRegistry;
    }

    /// <summary>
    /// Places the order in the book for its OutcomeId, runs matching, and for each match posts a double-entry Trade to the ledger.
    /// All ledger updates for matches in this call run in a single <see cref="IDbContextTransaction"/> (atomicity).
    /// If the order is from a Master (has followers), mirrors the order for each follower (one level only).
    /// </summary>
    public async Task<PlaceOrderResult> PlaceOrderAsync(Order order, CancellationToken cancellationToken = default)
    {
        return await PlaceOrderAsync(order, isMirrored: false, cancellationToken);
    }

    internal async Task<PlaceOrderResult> PlaceOrderAsync(Order order, bool isMirrored, CancellationToken cancellationToken = default)
    {
        if (order == null)
            throw new ArgumentNullException(nameof(order));

        if (_outcomeRegistry != null && !_outcomeRegistry.IsValid(order.OutcomeId))
            throw new InvalidOutcomeException(order.OutcomeId);

        var book = _orderBookStore.GetOrCreateOrderBook(order.OutcomeId);
        book.AddOrder(order);
        var matches = book.MatchOrders();

        var tradeTransactionIds = new List<Guid>();
        if (matches.Count > 0)
        {
            await using IDbContextTransaction dbTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                foreach (var match in matches)
                {
                    var buyerAccounts = await _accountRepository.GetByOperatorIdAsync(match.BuyerUserId, cancellationToken);
                    var sellerAccounts = await _accountRepository.GetByOperatorIdAsync(match.SellerUserId, cancellationToken);
                    if (buyerAccounts.Count == 0)
                        throw new InvalidOperationException($"Buyer {match.BuyerUserId} has no account. Create a wallet first.");
                    if (sellerAccounts.Count == 0)
                        throw new InvalidOperationException($"Seller {match.SellerUserId} has no account. Create a wallet first.");

                    var buyerAccountId = buyerAccounts[0].Id;
                    var sellerAccountId = sellerAccounts[0].Id;
                    decimal amount = match.Price * match.Quantity;

                    var buyerBalance = await _ledgerService.GetAccountBalanceAsync(buyerAccountId, SettlementPhase.Clearing, cancellationToken);
                    if (buyerBalance < amount)
                        throw new InsufficientFundsException(amount, buyerBalance);

                    // Buyer pays: Credit buyer (balance decreases). Seller receives: Debit seller (balance increases).
                    var entries = new List<JournalEntry>
                    {
                        new(buyerAccountId, amount, EntryType.Credit, SettlementPhase.Clearing),
                        new(sellerAccountId, amount, EntryType.Debit, SettlementPhase.Clearing)
                    };

                    var txId = Guid.NewGuid();
                    var transaction = new Transaction(txId, entries, null, null, TransactionType.Trade);
                    await _transactionRepository.AppendAsync(transaction, cancellationToken);
                    tradeTransactionIds.Add(txId);

                    // Outcome ledger: BookTradeAsync (cash + outcome asset) using dynamic OutcomeAssetType from market
                    var outcomeAssetType = _outcomeAssetTypeResolver.GetOutcomeAssetType(order.OutcomeId);
                    await _accountingService.BookTradeAsync(
                        buyerAccountId,
                        sellerAccountId,
                        cashAmount: amount,
                        outcomeAssetType,
                        outcomeQuantity: match.Quantity,
                        timestamp: null,
                        cancellationToken);
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
                await dbTransaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await dbTransaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        if (!isMirrored && _copyTradingService.GetFollowers(order.UserId).Count > 0)
        {
            var mirroredOrders = await _copyTradingService.MirrorOrderAsync(order, cancellationToken);
            foreach (var mirrored in mirroredOrders)
            {
                var subResult = await PlaceOrderAsync(mirrored, isMirrored: true, cancellationToken);
                tradeTransactionIds.AddRange(subResult.TradeTransactionIds);
            }
        }

        return new PlaceOrderResult(order.OutcomeId, matches.Count, tradeTransactionIds);
    }

    /// <summary>Gets the order book for an outcome, or null if none exists.</summary>
    public OrderBook? GetOrderBook(string outcomeId) => _orderBookStore.GetOrderBook(outcomeId);

    /// <summary>Gets or creates the order book for an outcome (used when a market is opened).</summary>
    public OrderBook GetOrCreateOrderBook(string outcomeId) => _orderBookStore.GetOrCreateOrderBook(outcomeId);
}

/// <summary>Result of placing an order: outcome, number of matches, and ledger transaction IDs for each trade.</summary>
public record PlaceOrderResult(string OutcomeId, int MatchCount, IReadOnlyList<Guid> TradeTransactionIds);
