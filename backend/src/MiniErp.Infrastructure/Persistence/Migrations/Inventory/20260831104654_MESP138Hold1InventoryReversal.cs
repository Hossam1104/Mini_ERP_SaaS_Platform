using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Inventory
{
    /// <inheritdoc />
    public partial class MESP138Hold1InventoryReversal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReversalMovementIdsJson",
                schema: "inventory",
                table: "CustomerReturnLines",
                type: "nvarchar(max)",
                maxLength: 8192,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReversalMovementIdsJson",
                schema: "inventory",
                table: "CustomerReturnLines");
        }
    }
}
