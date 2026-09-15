using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Mesp141
{
    /// <inheritdoc />
    public partial class MESP141MasterReferenceExecution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MigrationExecutionBatches",
                schema: "migration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordType = table.Column<int>(type: "int", nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    OwnerBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MigrationExecutionBatches", x => x.Id);
                    table.UniqueConstraint("AK_MigrationExecutionBatches_TenantId_RunId_AttemptId_RecordType", x => new { x.TenantId, x.RunId, x.AttemptId, x.RecordType });
                    table.ForeignKey(
                        name: "FK_MigrationExecutionBatches_MigrationAttempts_TenantId_RunId_AttemptId",
                        columns: x => new { x.TenantId, x.RunId, x.AttemptId },
                        principalSchema: "migration",
                        principalTable: "MigrationAttempts",
                        principalColumns: new[] { "TenantId", "RunId", "AttemptId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MigrationExecutionBatches_MigrationRuns_TenantId_RunId",
                        columns: x => new { x.TenantId, x.RunId },
                        principalSchema: "migration",
                        principalTable: "MigrationRuns",
                        principalColumns: new[] { "TenantId", "RunId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MigrationExecutionEffects",
                schema: "migration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StagedRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceSequence = table.Column<int>(type: "int", nullable: false),
                    RecordType = table.Column<int>(type: "int", nullable: false),
                    OwnerBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerRowId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResultingResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResultingResourceCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Disposition = table.Column<int>(type: "int", nullable: false),
                    SafeCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EffectStartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MigrationExecutionEffects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MigrationExecutionEffects_MigrationExecutionBatches_TenantId_RunId_AttemptId_RecordType",
                        columns: x => new { x.TenantId, x.RunId, x.AttemptId, x.RecordType },
                        principalSchema: "migration",
                        principalTable: "MigrationExecutionBatches",
                        principalColumns: new[] { "TenantId", "RunId", "AttemptId", "RecordType" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MigrationExecutionEffects_MigrationStagedRecords_TenantId_RunId_StagedRecordId",
                        columns: x => new { x.TenantId, x.RunId, x.StagedRecordId },
                        principalSchema: "migration",
                        principalTable: "MigrationStagedRecords",
                        principalColumns: new[] { "TenantId", "RunId", "StagedRecordId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MigrationExecutionEffects_TenantId_RunId_AttemptId_RecordType",
                schema: "migration",
                table: "MigrationExecutionEffects",
                columns: new[] { "TenantId", "RunId", "AttemptId", "RecordType" });

            migrationBuilder.CreateIndex(
                name: "IX_MigrationExecutionBatches_TenantId_RunId_RecordType_Active",
                schema: "migration",
                table: "MigrationExecutionBatches",
                columns: new[] { "TenantId", "RunId", "RecordType" },
                unique: true,
                filter: "[State] IN (2, 3)");

            migrationBuilder.CreateIndex(
                name: "IX_MigrationExecutionEffects_TenantId_RunId_AttemptId_StagedRecordId",
                schema: "migration",
                table: "MigrationExecutionEffects",
                columns: new[] { "TenantId", "RunId", "AttemptId", "StagedRecordId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MigrationExecutionEffects_TenantId_RunId_StagedRecordId",
                schema: "migration",
                table: "MigrationExecutionEffects",
                columns: new[] { "TenantId", "RunId", "StagedRecordId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MigrationExecutionBatches_TenantId_RunId_RecordType_Active",
                schema: "migration",
                table: "MigrationExecutionBatches");

            migrationBuilder.DropTable(
                name: "MigrationExecutionEffects",
                schema: "migration");

            migrationBuilder.DropTable(
                name: "MigrationExecutionBatches",
                schema: "migration");
        }
    }
}
