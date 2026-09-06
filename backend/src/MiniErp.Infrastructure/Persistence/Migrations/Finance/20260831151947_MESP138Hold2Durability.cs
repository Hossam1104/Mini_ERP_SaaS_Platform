using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Finance
{
    /// <inheritdoc />
    public partial class MESP138Hold2Durability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AcknowledgedAt",
                schema: "finance",
                table: "CreditNotes",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                schema: "finance",
                table: "CreditNotes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DownstreamIdempotencyKey",
                schema: "finance",
                table: "CreditNotes",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EffectFingerprint",
                schema: "finance",
                table: "CreditNotes",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FinanceCommitState",
                schema: "finance",
                table: "CreditNotes",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "NotCommitted");

            migrationBuilder.AddColumn<Guid>(
                name: "FinanceEffectId",
                schema: "finance",
                table: "CreditNotes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastAttemptAt",
                schema: "finance",
                table: "CreditNotes",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                schema: "finance",
                table: "CreditNotes",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReconciliationState",
                schema: "finance",
                table: "CreditNotes",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<string>(
                name: "RequestFingerprint",
                schema: "finance",
                table: "CreditNotes",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReversalAcknowledgedAt",
                schema: "finance",
                table: "CreditNotes",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReversalAttemptCount",
                schema: "finance",
                table: "CreditNotes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ReversalDownstreamIdempotencyKey",
                schema: "finance",
                table: "CreditNotes",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReversalEffectFingerprint",
                schema: "finance",
                table: "CreditNotes",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReversalFinanceCommitState",
                schema: "finance",
                table: "CreditNotes",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "NotCommitted");

            migrationBuilder.AddColumn<Guid>(
                name: "ReversalFinanceEffectId",
                schema: "finance",
                table: "CreditNotes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReversalLastAttemptAt",
                schema: "finance",
                table: "CreditNotes",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReversalLastError",
                schema: "finance",
                table: "CreditNotes",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReversalReconciliationState",
                schema: "finance",
                table: "CreditNotes",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<string>(
                name: "ReversalRequestFingerprint",
                schema: "finance",
                table: "CreditNotes",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReversalSalesAcknowledgementState",
                schema: "finance",
                table: "CreditNotes",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "NotAcknowledged");

            migrationBuilder.AddColumn<string>(
                name: "SalesAcknowledgementState",
                schema: "finance",
                table: "CreditNotes",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "NotAcknowledged");

            migrationBuilder.AddColumn<string>(
                name: "SourceFingerprint",
                schema: "finance",
                table: "CreditNotes",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditNotes_TenantId_FinanceCommitState_ReconciliationState",
                schema: "finance",
                table: "CreditNotes",
                columns: new[] { "TenantId", "FinanceCommitState", "ReconciliationState" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CreditNotes_TenantId_FinanceCommitState_ReconciliationState",
                schema: "finance",
                table: "CreditNotes");

            migrationBuilder.DropColumn(
                name: "AcknowledgedAt",
                schema: "finance",
                table: "CreditNotes");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                schema: "finance",
                table: "CreditNotes");

            migrationBuilder.DropColumn(
                name: "DownstreamIdempotencyKey",
                schema: "finance",
                table: "CreditNotes");

            migrationBuilder.DropColumn(
                name: "EffectFingerprint",
                schema: "finance",
                table: "CreditNotes");

            migrationBuilder.DropColumn(
                name: "FinanceCommitState",
                schema: "finance",
                table: "CreditNotes");

            migrationBuilder.DropColumn(
                name: "FinanceEffectId",
                schema: "finance",
                table: "CreditNotes");

            migrationBuilder.DropColumn(
                name: "LastAttemptAt",
                schema: "finance",
                table: "CreditNotes");

            migrationBuilder.DropColumn(
                name: "LastError",
                schema: "finance",
                table: "CreditNotes");

            migrationBuilder.DropColumn(
                name: "ReconciliationState",
                schema: "finance",
                table: "CreditNotes");

            migrationBuilder.DropColumn(
                name: "RequestFingerprint",
                schema: "finance",
                table: "CreditNotes");

            migrationBuilder.DropColumn(
                name: "ReversalAcknowledgedAt",
                schema: "finance",
                table: "CreditNotes");

            migrationBuilder.DropColumn(
                name: "ReversalAttemptCount",
                schema: "finance",
                table: "CreditNotes");

            migrationBuilder.DropColumn(
                name: "ReversalDownstreamIdempotencyKey",
                schema: "finance",
                table: "CreditNotes");

            migrationBuilder.DropColumn(
                name: "ReversalEffectFingerprint",
                schema: "finance",
                table: "CreditNotes");

            migrationBuilder.DropColumn(
                name: "ReversalFinanceCommitState",
                schema: "finance",
                table: "CreditNotes");

            migrationBuilder.DropColumn(
                name: "ReversalFinanceEffectId",
                schema: "finance",
                table: "CreditNotes");

            migrationBuilder.DropColumn(
                name: "ReversalLastAttemptAt",
                schema: "finance",
                table: "CreditNotes");

            migrationBuilder.DropColumn(
                name: "ReversalLastError",
                schema: "finance",
                table: "CreditNotes");

            migrationBuilder.DropColumn(
                name: "ReversalReconciliationState",
                schema: "finance",
                table: "CreditNotes");

            migrationBuilder.DropColumn(
                name: "ReversalRequestFingerprint",
                schema: "finance",
                table: "CreditNotes");

            migrationBuilder.DropColumn(
                name: "ReversalSalesAcknowledgementState",
                schema: "finance",
                table: "CreditNotes");

            migrationBuilder.DropColumn(
                name: "SalesAcknowledgementState",
                schema: "finance",
                table: "CreditNotes");

            migrationBuilder.DropColumn(
                name: "SourceFingerprint",
                schema: "finance",
                table: "CreditNotes");
        }
    }
}
