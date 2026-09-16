using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Mesp141
{
    /// <inheritdoc />
    public partial class MESP141InventoryEconomicRepresentations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_MigrationExecutionEffects_TenantId_RunId_AttemptId_Id",
                schema: "migration",
                table: "MigrationExecutionEffects",
                columns: new[] { "TenantId", "RunId", "AttemptId", "Id" });

            migrationBuilder.CreateTable(
                name: "MigrationEconomicRepresentations",
                schema: "migration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EffectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerModule = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    EvidenceVersion = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EvidenceConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MigrationEconomicRepresentations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MigrationEconomicRepresentations_MigrationExecutionEffects_TenantId_RunId_AttemptId_EffectId",
                        columns: x => new { x.TenantId, x.RunId, x.AttemptId, x.EffectId },
                        principalSchema: "migration",
                        principalTable: "MigrationExecutionEffects",
                        principalColumns: new[] { "TenantId", "RunId", "AttemptId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MigrationEconomicRepresentations_TenantId_RunId_AttemptId_EffectId_OwnerModule_Kind_OwnerId_EvidenceVersion",
                schema: "migration",
                table: "MigrationEconomicRepresentations",
                columns: new[] { "TenantId", "RunId", "AttemptId", "EffectId", "OwnerModule", "Kind", "OwnerId", "EvidenceVersion" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MigrationEconomicRepresentations",
                schema: "migration");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_MigrationExecutionEffects_TenantId_RunId_AttemptId_Id",
                schema: "migration",
                table: "MigrationExecutionEffects");
        }
    }
}
