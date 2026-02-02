using Microsoft.AspNetCore.Mvc;
using ProjectExchange.Accounting.Domain.Abstractions;
using ProjectExchange.Accounting.Domain.Entities;
using ProjectExchange.Accounting.Domain.Enums;
using ProjectExchange.Accounting.Domain.Services;

namespace ProjectExchange.Core.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WalletController : ControllerBase
{
    private readonly LedgerService _ledgerService;
    private readonly IAccountRepository _accountRepository;

    public WalletController(LedgerService ledgerService, IAccountRepository accountRepository)
    {
        _ledgerService = ledgerService ?? throw new ArgumentNullException(nameof(ledgerService));
        _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
    }

    /// <summary>
    /// Creates a new account for an operator or user. Name is optional (defaults to "Wallet").
    /// </summary>
    [HttpPost("create")]
    public async Task<ActionResult<CreateWalletResponse>> CreateWallet(
        [FromBody] CreateWalletRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = string.IsNullOrWhiteSpace(request.Name) ? "Wallet" : request.Name.Trim();
        var id = request.Id ?? Guid.NewGuid();
        var account = new Account(id, name, AccountType.Asset, request.OperatorId);
        await _accountRepository.CreateAsync(account, cancellationToken);

        return CreatedAtAction(
            nameof(GetBalance),
            new { accountId = id },
            new CreateWalletResponse(id, account.Name, account.OperatorId));
    }

    /// <summary>
    /// Returns the current balance for an account using LedgerService, Clearing phase [2026-01-31].
    /// Route: GET /api/wallet/{id}/balance — id is the AccountId (e.g. wallet id from create). Balance is computed from ledger (InMemoryTransactionRepository) for that account.
    /// </summary>
    [HttpGet("{id:guid}/balance")]
    public async Task<ActionResult<GetBalanceResponse>> GetBalance(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var account = await _accountRepository.GetByIdAsync(id, cancellationToken);
        if (account == null)
            return NotFound($"Account {id} not found.");

        var balance = await _ledgerService.GetAccountBalanceAsync(
            id,
            SettlementPhase.Clearing,
            cancellationToken);
        return Ok(new GetBalanceResponse(id, balance, nameof(SettlementPhase.Clearing)));
    }
}

/// <summary>Request to create a wallet. Name is optional (defaults to "Wallet").</summary>
public record CreateWalletRequest(Guid OperatorId, string? Name = null, Guid? Id = null);
public record CreateWalletResponse(Guid Id, string Name, Guid OperatorId);
public record GetBalanceResponse(Guid AccountId, decimal Balance, string Phase);
