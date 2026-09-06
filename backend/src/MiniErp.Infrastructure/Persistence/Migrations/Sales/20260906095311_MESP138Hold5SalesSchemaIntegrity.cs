using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Sales
{
    /// <summary>
    /// MESP-138 HOLD-5 (HOLD-138-U). Sales migration-chain integrity repair.
    /// An earlier file, <c>20260828150000_MESP136SalesHold1.cs</c>, exists in history without valid
    /// EF Core migration metadata (no <c>[Migration]</c> attribute and no Designer target model), so EF
    /// never registered or applied it. Three columns that the current Sales model requires were therefore
    /// never created by any registered migration. This forward migration adds them idempotently so that a
    /// clean SQL Server installation converges with any environment that already carries them.
    /// The orphan file itself is intentionally left untouched.
    /// </summary>
    public partial class MESP138Hold5SalesSchemaIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Guarded with COL_LENGTH so a clean database receives the missing columns while a database that
            // already has them (for example one repaired manually, or created before the metadata was lost)
            // is left exactly as it is. Column types, nullability, and defaults mirror the current Sales model.
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[sales].[SalesOrders]', N'CurrentApprovalsJson') IS NULL
    ALTER TABLE [sales].[SalesOrders] ADD [CurrentApprovalsJson] nvarchar(max) NOT NULL DEFAULT N'[]';
");

            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[sales].[SalesOrders]', N'RevisionNumber') IS NULL
    ALTER TABLE [sales].[SalesOrders] ADD [RevisionNumber] int NOT NULL DEFAULT 1;
");

            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[sales].[SalesHistory]', N'SnapshotJson') IS NULL
    ALTER TABLE [sales].[SalesHistory] ADD [SnapshotJson] nvarchar(max) NULL;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally not reversible. Dropping SalesOrders.CurrentApprovalsJson,
            // SalesOrders.RevisionNumber, or SalesHistory.SnapshotJson would delete live Sales approval,
            // revision, and history snapshot evidence and would leave the physical schema behind the current
            // Sales model. This migration only repairs missing schema, so reverting it is a no-op.
        }
    }
}
