using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Sales
{
    /// <inheritdoc />
    public partial class MESP138Hold1Remediation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ActiveFinanceCreditNoteCount",
                schema: "sales",
                table: "SalesCustomerReturns",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FinanceCreditNoteIdsJson",
                schema: "sales",
                table: "SalesCustomerReturns",
                type: "nvarchar(max)",
                maxLength: 8192,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FinanceEffectState",
                schema: "sales",
                table: "SalesCustomerReturns",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "InventoryAcknowledgementState",
                schema: "sales",
                table: "SalesCustomerReturns",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "InventoryAttemptCount",
                schema: "sales",
                table: "SalesCustomerReturns",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "InventoryCommitState",
                schema: "sales",
                table: "SalesCustomerReturns",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "InventoryCorrelationId",
                schema: "sales",
                table: "SalesCustomerReturns",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "InventoryEffectFingerprint",
                schema: "sales",
                table: "SalesCustomerReturns",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InventoryEffectId",
                schema: "sales",
                table: "SalesCustomerReturns",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InventoryInspectionEvidenceReference",
                schema: "sales",
                table: "SalesCustomerReturns",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "InventoryLastAttemptAt",
                schema: "sales",
                table: "SalesCustomerReturns",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InventoryLastError",
                schema: "sales",
                table: "SalesCustomerReturns",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InventoryPhysicalEvidenceReference",
                schema: "sales",
                table: "SalesCustomerReturns",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "InventoryReconciliationState",
                schema: "sales",
                table: "SalesCustomerReturns",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "InventoryRequestFingerprint",
                schema: "sales",
                table: "SalesCustomerReturns",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CommerciallyAcceptedQuantity",
                schema: "sales",
                table: "SalesCustomerReturnLines",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryMovementIdsJson",
                schema: "sales",
                table: "SalesCustomerReturnLines",
                type: "nvarchar(max)",
                maxLength: 8192,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "DeliveryUnitCost",
                schema: "sales",
                table: "SalesCustomerReturnLines",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InspectedQuantity",
                schema: "sales",
                table: "SalesCustomerReturnLines",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "InventoryMovementIdsJson",
                schema: "sales",
                table: "SalesCustomerReturnLines",
                type: "nvarchar(max)",
                maxLength: 8192,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "NonRestockableAcceptedQuantity",
                schema: "sales",
                table: "SalesCustomerReturnLines",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ReceivedQuantity",
                schema: "sales",
                table: "SalesCustomerReturnLines",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RejectedQuantity",
                schema: "sales",
                table: "SalesCustomerReturnLines",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestockedQuantity",
                schema: "sales",
                table: "SalesCustomerReturnLines",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "StockDisposition",
                schema: "sales",
                table: "SalesCustomerReturnLines",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "SalesCustomerReturnInvoiceAllocations",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerReturnId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinanceOpenItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeliveryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderRevisionNumber = table.Column<int>(type: "int", nullable: false),
                    RecognizedQuantity = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    ReturnQuantity = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    CommerciallyAcceptedQuantity = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    PreviouslyCreditedQuantity = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    RemainingCreditableQuantity = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    NetAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    GrossAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    TaxId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TaxRateVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TaxRateVersionNumber = table.Column<int>(type: "int", nullable: true),
                    SourceAllocationFingerprint = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SourceInvoiceFingerprint = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesCustomerReturnInvoiceAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesCustomerReturnInvoiceAllocations_SalesCustomerReturns_TenantId_CustomerReturnId",
                        columns: x => new { x.TenantId, x.CustomerReturnId },
                        principalSchema: "sales",
                        principalTable: "SalesCustomerReturns",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalesCustomerReturns_TenantId_InventoryEffectId",
                schema: "sales",
                table: "SalesCustomerReturns",
                columns: new[] { "TenantId", "InventoryEffectId" },
                unique: true,
                filter: "[InventoryEffectId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SalesCustomerReturnInvoiceAllocations_TenantId_CustomerReturnId_Id",
                schema: "sales",
                table: "SalesCustomerReturnInvoiceAllocations",
                columns: new[] { "TenantId", "CustomerReturnId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesCustomerReturnInvoiceAllocations_TenantId_CustomerReturnId_InvoiceId_OrderLineId",
                schema: "sales",
                table: "SalesCustomerReturnInvoiceAllocations",
                columns: new[] { "TenantId", "CustomerReturnId", "InvoiceId", "OrderLineId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesCustomerReturnInvoiceAllocations_TenantId_Id",
                schema: "sales",
                table: "SalesCustomerReturnInvoiceAllocations",
                columns: new[] { "TenantId", "Id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SalesCustomerReturnInvoiceAllocations",
                schema: "sales");

            migrationBuilder.DropIndex(
                name: "IX_SalesCustomerReturns_TenantId_InventoryEffectId",
                schema: "sales",
                table: "SalesCustomerReturns");

            migrationBuilder.DropColumn(
                name: "ActiveFinanceCreditNoteCount",
                schema: "sales",
                table: "SalesCustomerReturns");

            migrationBuilder.DropColumn(
                name: "FinanceCreditNoteIdsJson",
                schema: "sales",
                table: "SalesCustomerReturns");

            migrationBuilder.DropColumn(
                name: "FinanceEffectState",
                schema: "sales",
                table: "SalesCustomerReturns");

            migrationBuilder.DropColumn(
                name: "InventoryAcknowledgementState",
                schema: "sales",
                table: "SalesCustomerReturns");

            migrationBuilder.DropColumn(
                name: "InventoryAttemptCount",
                schema: "sales",
                table: "SalesCustomerReturns");

            migrationBuilder.DropColumn(
                name: "InventoryCommitState",
                schema: "sales",
                table: "SalesCustomerReturns");

            migrationBuilder.DropColumn(
                name: "InventoryCorrelationId",
                schema: "sales",
                table: "SalesCustomerReturns");

            migrationBuilder.DropColumn(
                name: "InventoryEffectFingerprint",
                schema: "sales",
                table: "SalesCustomerReturns");

            migrationBuilder.DropColumn(
                name: "InventoryEffectId",
                schema: "sales",
                table: "SalesCustomerReturns");

            migrationBuilder.DropColumn(
                name: "InventoryInspectionEvidenceReference",
                schema: "sales",
                table: "SalesCustomerReturns");

            migrationBuilder.DropColumn(
                name: "InventoryLastAttemptAt",
                schema: "sales",
                table: "SalesCustomerReturns");

            migrationBuilder.DropColumn(
                name: "InventoryLastError",
                schema: "sales",
                table: "SalesCustomerReturns");

            migrationBuilder.DropColumn(
                name: "InventoryPhysicalEvidenceReference",
                schema: "sales",
                table: "SalesCustomerReturns");

            migrationBuilder.DropColumn(
                name: "InventoryReconciliationState",
                schema: "sales",
                table: "SalesCustomerReturns");

            migrationBuilder.DropColumn(
                name: "InventoryRequestFingerprint",
                schema: "sales",
                table: "SalesCustomerReturns");

            migrationBuilder.DropColumn(
                name: "CommerciallyAcceptedQuantity",
                schema: "sales",
                table: "SalesCustomerReturnLines");

            migrationBuilder.DropColumn(
                name: "DeliveryMovementIdsJson",
                schema: "sales",
                table: "SalesCustomerReturnLines");

            migrationBuilder.DropColumn(
                name: "DeliveryUnitCost",
                schema: "sales",
                table: "SalesCustomerReturnLines");

            migrationBuilder.DropColumn(
                name: "InspectedQuantity",
                schema: "sales",
                table: "SalesCustomerReturnLines");

            migrationBuilder.DropColumn(
                name: "InventoryMovementIdsJson",
                schema: "sales",
                table: "SalesCustomerReturnLines");

            migrationBuilder.DropColumn(
                name: "NonRestockableAcceptedQuantity",
                schema: "sales",
                table: "SalesCustomerReturnLines");

            migrationBuilder.DropColumn(
                name: "ReceivedQuantity",
                schema: "sales",
                table: "SalesCustomerReturnLines");

            migrationBuilder.DropColumn(
                name: "RejectedQuantity",
                schema: "sales",
                table: "SalesCustomerReturnLines");

            migrationBuilder.DropColumn(
                name: "RestockedQuantity",
                schema: "sales",
                table: "SalesCustomerReturnLines");

            migrationBuilder.DropColumn(
                name: "StockDisposition",
                schema: "sales",
                table: "SalesCustomerReturnLines");
        }
    }
}
