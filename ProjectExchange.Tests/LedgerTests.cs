using ProjectExchange.Accounting.Domain.Abstractions;
using ProjectExchange.Accounting.Domain.Entities;
using ProjectExchange.Accounting.Domain.Enums;
using ProjectExchange.Accounting.Domain.Exceptions;
using ProjectExchange.Accounting.Domain.Services;

namespace ProjectExchange.Tests;

/// <summary>
/// Tests for the double-entry ledger: balanced transactions, rejection of unbalanced ones,
/// and integrity of balances after multiple trades. Uses EF Core InMemory and enterprise repositories.
/// </summary>
public class LedgerTests
{
    private static (IAccountRepository AccountRepo, ITransactionRepository TransactionRepo, LedgerService LedgerService) CreateLedger() =>
        EnterpriseTestSetup.CreateLedger();

    // --- Success: standard balanced transaction ---

    [Fact]
    public async Task Transaction_WithEqualDebitAndCredit_HasZeroNetImpactOnSystem()
    {
        var (accountRepo, _, ledgerService) = CreateLedger();

        var operatorId = Guid.NewGuid();
        var accountA = new Account(Guid.NewGuid(), "Account A", AccountType.Asset, operatorId.ToString());
        var accountB = new Account(Guid.NewGuid(), "Account B", AccountType.Asset, operatorId.ToString());
        await accountRepo.CreateAsync(accountA);
        await accountRepo.CreateAsync(accountB);

        const decimal amount = 100.50m;
        var entries = new List<JournalEntry>
        {
            new(accountA.Id, amount, EntryType.Debit, SettlementPhase.Clearing),
            new(accountB.Id, amount, EntryType.Credit, SettlementPhase.Clearing)
        };

        await ledgerService.PostTransactionAsync(entries);

        var balanceA = await ledgerService.GetAccountBalanceAsync(accountA.Id, null);
        var balanceB = await ledgerService.GetAccountBalanceAsync(accountB.Id, null);
        var netImpact = balanceA + balanceB;

        Assert.Equal(amount, balanceA);
        Assert.Equal(-amount, balanceB);
        Assert.Equal(0, netImpact);
    }

    // --- Security: unbalanced transaction must be rejected ---

    [Fact]
    public async Task PostTransaction_WithOnlyDebit_ThrowsTransactionNotBalancedException()
    {
        var (accountRepo, _, ledgerService) = CreateLedger();

        var operatorId = Guid.NewGuid();
        var accountA = new Account(Guid.NewGuid(), "Account A", AccountType.Asset, operatorId.ToString());
        await accountRepo.CreateAsync(accountA);

        var entries = new List<JournalEntry>
        {
            new(accountA.Id, 100m, EntryType.Debit, SettlementPhase.Clearing)
        };

        var ex = await Assert.ThrowsAsync<TransactionNotBalancedException>(
            () => ledgerService.PostTransactionAsync(entries));

        Assert.Equal(100m, ex.TotalDebits);
        Assert.Equal(0m, ex.TotalCredits);
    }

    [Fact]
    public async Task PostTransaction_WithDebitNotEqualToCredit_ThrowsTransactionNotBalancedException()
    {
        var (accountRepo, _, ledgerService) = CreateLedger();

        var operatorId = Guid.NewGuid();
        var accountA = new Account(Guid.NewGuid(), "Account A", AccountType.Asset, operatorId.ToString());
        var accountB = new Account(Guid.NewGuid(), "Account B", AccountType.Asset, operatorId.ToString());
        await accountRepo.CreateAsync(accountA);
        await accountRepo.CreateAsync(accountB);

        var entries = new List<JournalEntry>
        {
            new(accountA.Id, 100m, EntryType.Debit, SettlementPhase.Clearing),
            new(accountB.Id, 50m, EntryType.Credit, SettlementPhase.Clearing)
        };

        var ex = await Assert.ThrowsAsync<TransactionNotBalancedException>(
            () => ledgerService.PostTransactionAsync(entries));

        Assert.Equal(100m, ex.TotalDebits);
        Assert.Equal(50m, ex.TotalCredits);
    }

    // --- Integrity: GetAccountBalanceAsync after multiple trades ---

    [Fact]
    public async Task GetAccountBalanceAsync_AfterMultipleTrades_ReturnsExpectedBalances()
    {
        var (accountRepo, _, ledgerService) = CreateLedger();

        var operatorId = Guid.NewGuid();
        var accountA = new Account(Guid.NewGuid(), "Account A", AccountType.Asset, operatorId.ToString());
        var accountB = new Account(Guid.NewGuid(), "Account B", AccountType.Asset, operatorId.ToString());
        await accountRepo.CreateAsync(accountA);
        await accountRepo.CreateAsync(accountB);

        // Trade 1: A debits 100, B credits 100
        await ledgerService.PostTransactionAsync(new List<JournalEntry>
        {
            new(accountA.Id, 100m, EntryType.Debit, SettlementPhase.Clearing),
            new(accountB.Id, 100m, EntryType.Credit, SettlementPhase.Clearing)
        });

        // Trade 2: A debits 50, B credits 50
        await ledgerService.PostTransactionAsync(new List<JournalEntry>
        {
            new(accountA.Id, 50m, EntryType.Debit, SettlementPhase.Clearing),
            new(accountB.Id, 50m, EntryType.Credit, SettlementPhase.Clearing)
        });

        // Trade 3: reverse 30 from A to B (B debits 30, A credits 30)
        await ledgerService.PostTransactionAsync(new List<JournalEntry>
        {
            new(accountB.Id, 30m, EntryType.Debit, SettlementPhase.Clearing),
            new(accountA.Id, 30m, EntryType.Credit, SettlementPhase.Clearing)
        });

        var balanceA = await ledgerService.GetAccountBalanceAsync(accountA.Id, null);
        var balanceB = await ledgerService.GetAccountBalanceAsync(accountB.Id, null);

        // A: +100 + 50 - 30 = 120; B: -100 - 50 + 30 = -120
        Assert.Equal(120m, balanceA);
        Assert.Equal(-120m, balanceB);
        Assert.Equal(0m, balanceA + balanceB);
    }

    [Fact]
    public async Task GetAccountBalanceAsync_WithPhaseFilter_ReturnsOnlyEntriesForThatPhase()
    {
        var (accountRepo, _, ledgerService) = CreateLedger();

        var operatorId = Guid.NewGuid();
        var accountA = new Account(Guid.NewGuid(), "Account A", AccountType.Asset, operatorId.ToString());
        var accountB = new Account(Guid.NewGuid(), "Account B", AccountType.Asset, operatorId.ToString());
        await accountRepo.CreateAsync(accountA);
        await accountRepo.CreateAsync(accountB);

        await ledgerService.PostTransactionAsync(new List<JournalEntry>
        {
            new(accountA.Id, 80m, EntryType.Debit, SettlementPhase.Clearing),
            new(accountB.Id, 80m, EntryType.Credit, SettlementPhase.Clearing)
        });

        await ledgerService.PostTransactionAsync(new List<JournalEntry>
        {
            new(accountA.Id, 20m, EntryType.Credit, SettlementPhase.Settlement),
            new(accountB.Id, 20m, EntryType.Debit, SettlementPhase.Settlement)
        });

        var clearingBalanceA = await ledgerService.GetAccountBalanceAsync(accountA.Id, SettlementPhase.Clearing);
        var settlementBalanceA = await ledgerService.GetAccountBalanceAsync(accountA.Id, SettlementPhase.Settlement);
        var totalBalanceA = await ledgerService.GetAccountBalanceAsync(accountA.Id, null);

        Assert.Equal(80m, clearingBalanceA);
        Assert.Equal(-20m, settlementBalanceA);
        Assert.Equal(60m, totalBalanceA);
    }
}
