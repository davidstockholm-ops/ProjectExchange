using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectExchange.Core.Migrations
{
    /// <inheritdoc />
    public partial class FixNamingConventions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop FK and indexes that reference old names
            migrationBuilder.DropForeignKey(
                name: "FK_JournalEntries_Transactions_TransactionId",
                table: "JournalEntries");

            migrationBuilder.DropIndex(
                name: "IX_JournalEntries_AccountId",
                table: "JournalEntries");
            migrationBuilder.DropIndex(
                name: "IX_JournalEntries_TransactionId",
                table: "JournalEntries");

            migrationBuilder.DropIndex(
                name: "IX_LedgerEntries_AccountId",
                table: "LedgerEntries");
            migrationBuilder.DropIndex(
                name: "IX_LedgerEntries_Timestamp",
                table: "LedgerEntries");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_OperatorId_Name",
                table: "Accounts");

            // Rename tables to snake_case
            migrationBuilder.RenameTable(
                name: "Transactions",
                newName: "transactions");
            migrationBuilder.RenameTable(
                name: "Accounts",
                newName: "accounts");
            migrationBuilder.RenameTable(
                name: "JournalEntries",
                newName: "journal_entries");
            migrationBuilder.RenameTable(
                name: "LedgerEntries",
                newName: "ledger_entries");

            // Rename columns in transactions
            migrationBuilder.RenameColumn(
                name: "Id",
                table: "transactions",
                newName: "id");
            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "transactions",
                newName: "created_at");
            migrationBuilder.RenameColumn(
                name: "SettlesClearingTransactionId",
                table: "transactions",
                newName: "settles_clearing_transaction_id");
            migrationBuilder.RenameColumn(
                name: "Type",
                table: "transactions",
                newName: "type");

            // Rename columns in accounts
            migrationBuilder.RenameColumn(
                name: "Id",
                table: "accounts",
                newName: "id");
            migrationBuilder.RenameColumn(
                name: "Name",
                table: "accounts",
                newName: "name");
            migrationBuilder.RenameColumn(
                name: "Type",
                table: "accounts",
                newName: "type");
            migrationBuilder.RenameColumn(
                name: "OperatorId",
                table: "accounts",
                newName: "operator_id");

            // Rename columns in journal_entries
            migrationBuilder.RenameColumn(
                name: "Id",
                table: "journal_entries",
                newName: "id");
            migrationBuilder.RenameColumn(
                name: "TransactionId",
                table: "journal_entries",
                newName: "transaction_id");
            migrationBuilder.RenameColumn(
                name: "AccountId",
                table: "journal_entries",
                newName: "account_id");
            migrationBuilder.RenameColumn(
                name: "Amount",
                table: "journal_entries",
                newName: "amount");
            migrationBuilder.RenameColumn(
                name: "EntryType",
                table: "journal_entries",
                newName: "entry_type");
            migrationBuilder.RenameColumn(
                name: "Phase",
                table: "journal_entries",
                newName: "phase");

            // Rename columns in ledger_entries
            migrationBuilder.RenameColumn(
                name: "Id",
                table: "ledger_entries",
                newName: "id");
            migrationBuilder.RenameColumn(
                name: "AccountId",
                table: "ledger_entries",
                newName: "account_id");
            migrationBuilder.RenameColumn(
                name: "AssetType",
                table: "ledger_entries",
                newName: "asset_type");
            migrationBuilder.RenameColumn(
                name: "Amount",
                table: "ledger_entries",
                newName: "amount");
            migrationBuilder.RenameColumn(
                name: "Direction",
                table: "ledger_entries",
                newName: "direction");
            migrationBuilder.RenameColumn(
                name: "Timestamp",
                table: "ledger_entries",
                newName: "timestamp");

            // Recreate primary keys (PostgreSQL may need PK name updates; RenameColumn doesn't change PK constraint name)
            // Recreate indexes
            migrationBuilder.CreateIndex(
                name: "IX_accounts_operator_id_name",
                table: "accounts",
                columns: new[] { "operator_id", "name" });

            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_account_id",
                table: "journal_entries",
                column: "account_id");
            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_transaction_id",
                table: "journal_entries",
                column: "transaction_id");

            migrationBuilder.AddForeignKey(
                name: "FK_journal_entries_transactions_transaction_id",
                table: "journal_entries",
                column: "transaction_id",
                principalTable: "transactions",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_account_id",
                table: "ledger_entries",
                column: "account_id");
            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_timestamp",
                table: "ledger_entries",
                column: "timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_journal_entries_transactions_transaction_id",
                table: "journal_entries");

            migrationBuilder.DropIndex(
                name: "IX_accounts_operator_id_name",
                table: "accounts");
            migrationBuilder.DropIndex(
                name: "IX_journal_entries_account_id",
                table: "journal_entries");
            migrationBuilder.DropIndex(
                name: "IX_journal_entries_transaction_id",
                table: "journal_entries");
            migrationBuilder.DropIndex(
                name: "IX_ledger_entries_account_id",
                table: "ledger_entries");
            migrationBuilder.DropIndex(
                name: "IX_ledger_entries_timestamp",
                table: "ledger_entries");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "ledger_entries",
                newName: "Id");
            migrationBuilder.RenameColumn(
                name: "account_id",
                table: "ledger_entries",
                newName: "AccountId");
            migrationBuilder.RenameColumn(
                name: "asset_type",
                table: "ledger_entries",
                newName: "AssetType");
            migrationBuilder.RenameColumn(
                name: "amount",
                table: "ledger_entries",
                newName: "Amount");
            migrationBuilder.RenameColumn(
                name: "direction",
                table: "ledger_entries",
                newName: "Direction");
            migrationBuilder.RenameColumn(
                name: "timestamp",
                table: "ledger_entries",
                newName: "Timestamp");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "journal_entries",
                newName: "Id");
            migrationBuilder.RenameColumn(
                name: "transaction_id",
                table: "journal_entries",
                newName: "TransactionId");
            migrationBuilder.RenameColumn(
                name: "account_id",
                table: "journal_entries",
                newName: "AccountId");
            migrationBuilder.RenameColumn(
                name: "amount",
                table: "journal_entries",
                newName: "Amount");
            migrationBuilder.RenameColumn(
                name: "entry_type",
                table: "journal_entries",
                newName: "EntryType");
            migrationBuilder.RenameColumn(
                name: "phase",
                table: "journal_entries",
                newName: "Phase");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "accounts",
                newName: "Id");
            migrationBuilder.RenameColumn(
                name: "name",
                table: "accounts",
                newName: "Name");
            migrationBuilder.RenameColumn(
                name: "type",
                table: "accounts",
                newName: "Type");
            migrationBuilder.RenameColumn(
                name: "operator_id",
                table: "accounts",
                newName: "OperatorId");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "transactions",
                newName: "Id");
            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "transactions",
                newName: "CreatedAt");
            migrationBuilder.RenameColumn(
                name: "settles_clearing_transaction_id",
                table: "transactions",
                newName: "SettlesClearingTransactionId");
            migrationBuilder.RenameColumn(
                name: "type",
                table: "transactions",
                newName: "Type");

            migrationBuilder.RenameTable(
                name: "ledger_entries",
                newName: "LedgerEntries");
            migrationBuilder.RenameTable(
                name: "journal_entries",
                newName: "JournalEntries");
            migrationBuilder.RenameTable(
                name: "accounts",
                newName: "Accounts");
            migrationBuilder.RenameTable(
                name: "transactions",
                newName: "Transactions");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_OperatorId_Name",
                table: "Accounts",
                columns: new[] { "OperatorId", "Name" });
            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_AccountId",
                table: "JournalEntries",
                column: "AccountId");
            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_TransactionId",
                table: "JournalEntries",
                column: "TransactionId");
            migrationBuilder.AddForeignKey(
                name: "FK_JournalEntries_Transactions_TransactionId",
                table: "JournalEntries",
                column: "TransactionId",
                principalTable: "Transactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_AccountId",
                table: "LedgerEntries",
                column: "AccountId");
            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_Timestamp",
                table: "LedgerEntries",
                column: "Timestamp");
        }
    }
}
