using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Sales
{
    /// <inheritdoc />
    public partial class MESP138Hold3FinanceEffectAuthority : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SalesCustomerReturnFinanceEffects",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerReturnId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreditNoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinanceOpenItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PostingJournalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaxJournalIdsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8192, nullable: false),
                    NetAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    GrossAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SourceAllocationIdsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8192, nullable: false),
                    SourceFingerprint = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    EffectFingerprint = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RequestFingerprint = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DownstreamIdempotencyKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    State = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AcknowledgedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReversalState = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ReversalJournalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReversalEffectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReversalEffectFingerprint = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ReversalRequestFingerprint = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ReversalDownstreamIdempotencyKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ReversedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesCustomerReturnFinanceEffects", x => x.Id);
                    table.UniqueConstraint("AK_SalesCustomerReturnFinanceEffects_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_SalesCustomerReturnFinanceEffects_SalesCustomerReturns_TenantId_CustomerReturnId",
                        columns: x => new { x.TenantId, x.CustomerReturnId },
                        principalSchema: "sales",
                        principalTable: "SalesCustomerReturns",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SalesCustomerReturnFinanceEffectAllocations",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinanceEffectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceAllocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    NetAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    GrossAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    SourceAllocationFingerprint = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesCustomerReturnFinanceEffectAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesCustomerReturnFinanceEffectAllocations_SalesCustomerReturnFinanceEffects_TenantId_FinanceEffectId",
                        columns: x => new { x.TenantId, x.FinanceEffectId },
                        principalSchema: "sales",
                        principalTable: "SalesCustomerReturnFinanceEffects",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalesCustomerReturnFinanceEffectAllocations_TenantId_FinanceEffectId_SourceAllocationId",
                schema: "sales",
                table: "SalesCustomerReturnFinanceEffectAllocations",
                columns: new[] { "TenantId", "FinanceEffectId", "SourceAllocationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesCustomerReturnFinanceEffectAllocations_TenantId_Id",
                schema: "sales",
                table: "SalesCustomerReturnFinanceEffectAllocations",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesCustomerReturnFinanceEffectAllocations_TenantId_SourceAllocationId",
                schema: "sales",
                table: "SalesCustomerReturnFinanceEffectAllocations",
                columns: new[] { "TenantId", "SourceAllocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesCustomerReturnFinanceEffects_TenantId_CustomerReturnId_CreditNoteId",
                schema: "sales",
                table: "SalesCustomerReturnFinanceEffects",
                columns: new[] { "TenantId", "CustomerReturnId", "CreditNoteId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesCustomerReturnFinanceEffects_TenantId_CustomerReturnId_InvoiceId_State",
                schema: "sales",
                table: "SalesCustomerReturnFinanceEffects",
                columns: new[] { "TenantId", "CustomerReturnId", "InvoiceId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesCustomerReturnFinanceEffects_TenantId_Id",
                schema: "sales",
                table: "SalesCustomerReturnFinanceEffects",
                columns: new[] { "TenantId", "Id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SalesCustomerReturnFinanceEffectAllocations",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "SalesCustomerReturnFinanceEffects",
                schema: "sales");
        }
    }
}
