using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Inventory
{
    /// <inheritdoc />
    public partial class MESP138CustomerReturnFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerReturns",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SalesCustomerReturnId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PhysicalEvidenceReference = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    InspectionEvidenceReference = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    HandoffState = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ReceiptDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PostedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerReturns", x => x.Id);
                    table.UniqueConstraint("AK_CustomerReturns_TenantId_Id", x => new { x.TenantId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "CustomerReturnLines",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryCustomerReturnId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceivedQuantity = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    DispositionedQuantity = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    Disposition = table.Column<int>(type: "int", nullable: false),
                    MovementId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeliveryMovementId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeliveryUnitCost = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerReturnLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerReturnLines_CustomerReturns_TenantId_InventoryCustomerReturnId",
                        columns: x => new { x.TenantId, x.InventoryCustomerReturnId },
                        principalSchema: "inventory",
                        principalTable: "CustomerReturns",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReturnLines_TenantId_InventoryCustomerReturnId_OrderLineId",
                schema: "inventory",
                table: "CustomerReturnLines",
                columns: new[] { "TenantId", "InventoryCustomerReturnId", "OrderLineId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReturns_TenantId_SalesCustomerReturnId",
                schema: "inventory",
                table: "CustomerReturns",
                columns: new[] { "TenantId", "SalesCustomerReturnId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerReturnLines",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "CustomerReturns",
                schema: "inventory");
        }
    }
}
