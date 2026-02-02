using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectExchange.Core.Migrations
{
    /// <inheritdoc />
    public partial class AccountOperatorIdToString : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Accounts_OperatorId_Name",
                table: "Accounts");

            migrationBuilder.AlterColumn<string>(
                name: "OperatorId",
                table: "Accounts",
                type: "character varying(450)",
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_OperatorId_Name",
                table: "Accounts",
                columns: new[] { "OperatorId", "Name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Accounts_OperatorId_Name",
                table: "Accounts");

            migrationBuilder.AlterColumn<Guid>(
                name: "OperatorId",
                table: "Accounts",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(450)",
                oldMaxLength: 450);

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_OperatorId_Name",
                table: "Accounts",
                columns: new[] { "OperatorId", "Name" });
        }
    }
}
