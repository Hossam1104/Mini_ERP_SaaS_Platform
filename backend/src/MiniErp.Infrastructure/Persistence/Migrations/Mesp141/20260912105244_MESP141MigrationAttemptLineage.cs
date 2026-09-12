using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Mesp141
{
    /// <summary>
    /// Additive reconciliation of the attempt retry lineage. The preceding
    /// migration left <c>PreviousAttemptId</c> as a loose Guid column with no
    /// referential constraint, so a row could name a predecessor that did not
    /// exist or that belonged to another Tenant, and the lineage BRD section
    /// 13.8 depends on was not actually guaranteed by the database. The unique
    /// index on (TenantId, AttemptId) becomes a real unique constraint because
    /// a Tenant-qualified self reference needs a principal key, not an index.
    /// </summary>
    /// <remarks>
    /// This is deliberately a second migration rather than an edit of
    /// <c>20260911183345_MESP141MigrationFoundation</c>: that one is already
    /// pushed, and rewriting an applied migration would leave any database
    /// that ran it silently inconsistent with its own history table. Nothing
    /// here drops a column or a row, so it is safe to apply forward.
    /// </remarks>
    public partial class MESP141MigrationAttemptLineage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MigrationAttempts_TenantId_AttemptId",
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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

            migrationBuilder.CreateIndex(
                name: "IX_MigrationAttempts_TenantId_AttemptId",
                schema: "migration",
                table: "MigrationAttempts",
                columns: new[] { "TenantId", "AttemptId" },
                unique: true);
        }
    }
}
