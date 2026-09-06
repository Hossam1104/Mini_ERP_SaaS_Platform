using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Sales
{
    /// <inheritdoc />
    public partial class MESP138Hold2FinanceReversalAck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FinanceReversedCreditNoteIdsJson",
                schema: "sales",
                table: "SalesCustomerReturns",
                type: "nvarchar(max)",
                maxLength: 8192,
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FinanceReversedCreditNoteIdsJson",
                schema: "sales",
                table: "SalesCustomerReturns");
        }
    }
}
