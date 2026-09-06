using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Finance
{
    /// <inheritdoc />
    public partial class MESP138Hold1SourceEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CreditNotes_TenantId_SalesCustomerReturnId",
                schema: "finance",
                table: "CreditNotes");

            migrationBuilder.DropIndex(
                name: "IX_CreditNoteLines_TenantId_CreditNoteId_OrderLineId",
                schema: "finance",
                table: "CreditNoteLines");

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                schema: "finance",
                table: "CreditNotes",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExchangeRateId",
                schema: "finance",
                table: "CreditNotes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExchangeRateVersionId",
                schema: "finance",
                table: "CreditNotes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExchangeRateVersionNumber",
                schema: "finance",
                table: "CreditNotes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxReversalJournalIdsJson",
                schema: "finance",
                table: "CreditNotes",
                type: "nvarchar(max)",
                maxLength: 8192,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "RecognizedGrossAmount",
                schema: "finance",
                table: "CreditNoteLines",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RecognizedNetAmount",
                schema: "finance",
                table: "CreditNoteLines",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RecognizedQuantity",
                schema: "finance",
                table: "CreditNoteLines",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RecognizedTaxAmount",
                schema: "finance",
                table: "CreditNoteLines",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "SourceAllocationFingerprint",
                schema: "finance",
                table: "CreditNoteLines",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "SourceAllocationId",
                schema: "finance",
                table: "CreditNoteLines",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "TaxRateVersionNumber",
                schema: "finance",
                table: "CreditNoteLines",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditNotes_TenantId_SalesCustomerReturnId_InvoiceId",
                schema: "finance",
                table: "CreditNotes",
                columns: new[] { "TenantId", "SalesCustomerReturnId", "InvoiceId" },
                unique: true,
                filter: "[InvoiceId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CreditNoteLines_TenantId_CreditNoteId_SourceAllocationId",
                schema: "finance",
                table: "CreditNoteLines",
                columns: new[] { "TenantId", "CreditNoteId", "SourceAllocationId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CreditNotes_TenantId_SalesCustomerReturnId_InvoiceId",
                schema: "finance",
                table: "CreditNotes");

            migrationBuilder.DropIndex(
                name: "IX_CreditNoteLines_TenantId_CreditNoteId_SourceAllocationId",
                schema: "finance",
                table: "CreditNoteLines");

            migrationBuilder.DropColumn(
                name: "ExchangeRate",
                schema: "finance",
                table: "CreditNotes");

            migrationBuilder.DropColumn(
                name: "ExchangeRateId",
                schema: "finance",
                table: "CreditNotes");

            migrationBuilder.DropColumn(
                name: "ExchangeRateVersionId",
                schema: "finance",
                table: "CreditNotes");

            migrationBuilder.DropColumn(
                name: "ExchangeRateVersionNumber",
                schema: "finance",
                table: "CreditNotes");

            migrationBuilder.DropColumn(
                name: "TaxReversalJournalIdsJson",
                schema: "finance",
                table: "CreditNotes");

            migrationBuilder.DropColumn(
                name: "RecognizedGrossAmount",
                schema: "finance",
                table: "CreditNoteLines");

            migrationBuilder.DropColumn(
                name: "RecognizedNetAmount",
                schema: "finance",
                table: "CreditNoteLines");

            migrationBuilder.DropColumn(
                name: "RecognizedQuantity",
                schema: "finance",
                table: "CreditNoteLines");

            migrationBuilder.DropColumn(
                name: "RecognizedTaxAmount",
                schema: "finance",
                table: "CreditNoteLines");

            migrationBuilder.DropColumn(
                name: "SourceAllocationFingerprint",
                schema: "finance",
                table: "CreditNoteLines");

            migrationBuilder.DropColumn(
                name: "SourceAllocationId",
                schema: "finance",
                table: "CreditNoteLines");

            migrationBuilder.DropColumn(
                name: "TaxRateVersionNumber",
                schema: "finance",
                table: "CreditNoteLines");

            migrationBuilder.CreateIndex(
                name: "IX_CreditNotes_TenantId_SalesCustomerReturnId",
                schema: "finance",
                table: "CreditNotes",
                columns: new[] { "TenantId", "SalesCustomerReturnId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditNoteLines_TenantId_CreditNoteId_OrderLineId",
                schema: "finance",
                table: "CreditNoteLines",
                columns: new[] { "TenantId", "CreditNoteId", "OrderLineId" },
                unique: true);
        }
    }
}
