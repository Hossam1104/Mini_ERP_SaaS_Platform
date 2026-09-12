using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Mesp141
{
    /// <inheritdoc />
    public partial class MESP141MigrationRunQualifiedAttemptLineage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MigrationAttempts_MigrationAttempts_TenantId_PreviousAttemptId",
                schema: "migration",
                table: "MigrationAttempts");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_MigrationAttempts_TenantId_AttemptId",
                schema: "migration",
                table: "MigrationAttempts");

            migrationBuilder.DropIndex(
                name: "IX_MigrationAttempts_TenantId_PreviousAttemptId",
                schema: "migration",
                table: "MigrationAttempts");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_MigrationAttempts_TenantId_RunId_AttemptId",
                schema: "migration",
                table: "MigrationAttempts",
                columns: new[] { "TenantId", "RunId", "AttemptId" });

            migrationBuilder.CreateIndex(
                name: "IX_MigrationAttempts_TenantId_RunId_PreviousAttemptId",
                schema: "migration",
                table: "MigrationAttempts",
                columns: new[] { "TenantId", "RunId", "PreviousAttemptId" });

            migrationBuilder.AddForeignKey(
                name: "FK_MigrationAttempts_MigrationAttempts_TenantId_RunId_PreviousAttemptId",
                schema: "migration",
                table: "MigrationAttempts",
                columns: new[] { "TenantId", "RunId", "PreviousAttemptId" },
                principalSchema: "migration",
                principalTable: "MigrationAttempts",
                principalColumns: new[] { "TenantId", "RunId", "AttemptId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MigrationAttempts_MigrationAttempts_TenantId_RunId_PreviousAttemptId",
                schema: "migration",
                table: "MigrationAttempts");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_MigrationAttempts_TenantId_RunId_AttemptId",
                schema: "migration",
                table: "MigrationAttempts");

            migrationBuilder.DropIndex(
                name: "IX_MigrationAttempts_TenantId_RunId_PreviousAttemptId",
                schema: "migration",
                table: "MigrationAttempts");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_MigrationAttempts_TenantId_AttemptId",
                schema: "migration",
                table: "MigrationAttempts",
                columns: new[] { "TenantId", "AttemptId" });

            migrationBuilder.CreateIndex(
                name: "IX_MigrationAttempts_TenantId_PreviousAttemptId",
                schema: "migration",
                table: "MigrationAttempts",
                columns: new[] { "TenantId", "PreviousAttemptId" });

            migrationBuilder.AddForeignKey(
                name: "FK_MigrationAttempts_MigrationAttempts_TenantId_PreviousAttemptId",
                schema: "migration",
                table: "MigrationAttempts",
                columns: new[] { "TenantId", "PreviousAttemptId" },
                principalSchema: "migration",
                principalTable: "MigrationAttempts",
                principalColumns: new[] { "TenantId", "AttemptId" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
