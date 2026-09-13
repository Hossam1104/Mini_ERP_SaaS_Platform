using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Mesp141
{
    /// <inheritdoc />
    public partial class MESP141MigrationIntakeTenantInvariant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_MigrationIntakes_SourceTenant_Matches_Tenant",
                schema: "migration",
                table: "MigrationIntakes",
                sql: "[SourceTenantId] = [TenantId]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MigrationIntakes_SourceTenant_Matches_Tenant",
                schema: "migration",
                table: "MigrationIntakes");
        }
    }
}
