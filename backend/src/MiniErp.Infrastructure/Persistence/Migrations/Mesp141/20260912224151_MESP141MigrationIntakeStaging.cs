using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Mesp141
{
    /// <inheritdoc />
    public partial class MESP141MigrationIntakeStaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MigrationIntakes",
                schema: "migration",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Operation = table.Column<int>(type: "int", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    FingerprintVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RequestFingerprint = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SourceObjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceTenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceCompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceBranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceWarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SourceLength = table.Column<long>(type: "bigint", nullable: false),
                    SourceConcurrencyVersion = table.Column<long>(type: "bigint", nullable: false),
                    CapturedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MigrationIntakes", x => x.RunId);
                    table.ForeignKey(
                        name: "FK_MigrationIntakes_MigrationRuns_TenantId_RunId",
                        columns: x => new { x.TenantId, x.RunId },
                        principalSchema: "migration",
                        principalTable: "MigrationRuns",
                        principalColumns: new[] { "TenantId", "RunId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MigrationIntakes_TenantId_RunId",
                schema: "migration",
                table: "MigrationIntakes",
                columns: new[] { "TenantId", "RunId" });

            migrationBuilder.CreateIndex(
                name: "IX_MigrationIntakes_TenantId_SourceObjectId",
                schema: "migration",
                table: "MigrationIntakes",
                columns: new[] { "TenantId", "SourceObjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_MigrationIntakes_TenantId_IdempotencyKey",
                schema: "migration",
                table: "MigrationIntakes",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MigrationIntakes",
                schema: "migration");
        }
    }
}
