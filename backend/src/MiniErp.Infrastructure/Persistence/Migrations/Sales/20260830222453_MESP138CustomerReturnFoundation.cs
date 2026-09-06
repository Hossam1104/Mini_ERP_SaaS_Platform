using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Sales
{
    /// <inheritdoc />
    public partial class MESP138CustomerReturnFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SalesCustomerReturns",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeliveryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderRevisionNumber = table.Column<int>(type: "int", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FinanceOpenItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CurrencyCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Consequence = table.Column<int>(type: "int", nullable: false),
                    ReturnDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    EvidenceJson = table.Column<string>(type: "nvarchar(max)", maxLength: 65536, nullable: false),
                    HandoffJson = table.Column<string>(type: "nvarchar(max)", maxLength: 16384, nullable: false),
                    CreatedByActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesCustomerReturns", x => x.Id);
                    table.UniqueConstraint("AK_SalesCustomerReturns_TenantId_Id", x => new { x.TenantId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "SalesCustomerReturnLines",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerReturnId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeliveryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeliveredQuantity = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    PreviouslyReturnedQuantity = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    ReturnQuantity = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesCustomerReturnLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesCustomerReturnLines_SalesCustomerReturns_TenantId_CustomerReturnId",
                        columns: x => new { x.TenantId, x.CustomerReturnId },
                        principalSchema: "sales",
                        principalTable: "SalesCustomerReturns",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalesCustomerReturnLines_TenantId_CustomerReturnId_OrderLineId",
                schema: "sales",
                table: "SalesCustomerReturnLines",
                columns: new[] { "TenantId", "CustomerReturnId", "OrderLineId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesCustomerReturnLines_TenantId_DeliveryId_OrderLineId",
                schema: "sales",
                table: "SalesCustomerReturnLines",
                columns: new[] { "TenantId", "DeliveryId", "OrderLineId" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesCustomerReturnLines_TenantId_Id",
                schema: "sales",
                table: "SalesCustomerReturnLines",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesCustomerReturns_TenantId_DeliveryId_CreatedAt",
                schema: "sales",
                table: "SalesCustomerReturns",
                columns: new[] { "TenantId", "DeliveryId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesCustomerReturns_TenantId_FinanceOpenItemId_Status",
                schema: "sales",
                table: "SalesCustomerReturns",
                columns: new[] { "TenantId", "FinanceOpenItemId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesCustomerReturns_TenantId_Id",
                schema: "sales",
                table: "SalesCustomerReturns",
                columns: new[] { "TenantId", "Id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SalesCustomerReturnLines",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "SalesCustomerReturns",
                schema: "sales");
        }
    }
}
