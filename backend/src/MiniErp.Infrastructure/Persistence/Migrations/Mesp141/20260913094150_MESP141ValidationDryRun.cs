using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Mesp141
{
    /// <inheritdoc />
    public partial class MESP141ValidationDryRun : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MigrationDryRunPreviews",
                schema: "migration",
                columns: table => new
                {
                    PreviewId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ValidationAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PackageHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SourceSnapshotHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FindingCountsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 32000, nullable: false),
                    ControlTotalsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 32000, nullable: false),
                    UnresolvedDependencyCount = table.Column<int>(type: "int", nullable: false),
                    ExceptionCount = table.Column<int>(type: "int", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MigrationDryRunPreviews", x => x.PreviewId);
                    table.UniqueConstraint("AK_MigrationDryRunPreviews_TenantId_PreviewId", x => new { x.TenantId, x.PreviewId });
                    table.ForeignKey(
                        name: "FK_MigrationDryRunPreviews_MigrationAttempts_TenantId_RunId_AttemptId",
                        columns: x => new { x.TenantId, x.RunId, x.AttemptId },
                        principalSchema: "migration",
                        principalTable: "MigrationAttempts",
                        principalColumns: new[] { "TenantId", "RunId", "AttemptId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MigrationDryRunPreviews_MigrationAttempts_TenantId_RunId_ValidationAttemptId",
                        columns: x => new { x.TenantId, x.RunId, x.ValidationAttemptId },
                        principalSchema: "migration",
                        principalTable: "MigrationAttempts",
                        principalColumns: new[] { "TenantId", "RunId", "AttemptId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MigrationDryRunPreviews_MigrationRuns_TenantId_RunId",
                        columns: x => new { x.TenantId, x.RunId },
                        principalSchema: "migration",
                        principalTable: "MigrationRuns",
                        principalColumns: new[] { "TenantId", "RunId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MigrationStagedRecords",
                schema: "migration",
                columns: table => new
                {
                    StagedRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceSequence = table.Column<int>(type: "int", nullable: false),
                    SourceRecordId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RecordType = table.Column<int>(type: "int", nullable: false),
                    CanonicalPayload = table.Column<string>(type: "nvarchar(max)", maxLength: 2000000, nullable: false),
                    PayloadHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PackageHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PackageVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SourceObjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceSnapshotHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CapturedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MigrationStagedRecords", x => x.StagedRecordId);
                    table.UniqueConstraint("AK_MigrationStagedRecords_TenantId_RunId_StagedRecordId", x => new { x.TenantId, x.RunId, x.StagedRecordId });
                    table.ForeignKey(
                        name: "FK_MigrationStagedRecords_MigrationRuns_TenantId_RunId",
                        columns: x => new { x.TenantId, x.RunId },
                        principalSchema: "migration",
                        principalTable: "MigrationRuns",
                        principalColumns: new[] { "TenantId", "RunId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MigrationValidationFindings",
                schema: "migration",
                columns: table => new
                {
                    FindingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StagedRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    IsBlocking = table.Column<bool>(type: "bit", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    ReferenceId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MigrationValidationFindings", x => x.FindingId);
                    table.ForeignKey(
                        name: "FK_MigrationValidationFindings_MigrationAttempts_TenantId_RunId_AttemptId",
                        columns: x => new { x.TenantId, x.RunId, x.AttemptId },
                        principalSchema: "migration",
                        principalTable: "MigrationAttempts",
                        principalColumns: new[] { "TenantId", "RunId", "AttemptId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MigrationValidationFindings_MigrationRuns_TenantId_RunId",
                        columns: x => new { x.TenantId, x.RunId },
                        principalSchema: "migration",
                        principalTable: "MigrationRuns",
                        principalColumns: new[] { "TenantId", "RunId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MigrationValidationResults",
                schema: "migration",
                columns: table => new
                {
                    ValidationResultId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PackageHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SourceSnapshotHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FindingCountsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 32000, nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MigrationValidationResults", x => x.ValidationResultId);
                    table.UniqueConstraint("AK_MigrationValidationResults_TenantId_RunId_AttemptId", x => new { x.TenantId, x.RunId, x.AttemptId });
                    table.ForeignKey(
                        name: "FK_MigrationValidationResults_MigrationAttempts_TenantId_RunId_AttemptId",
                        columns: x => new { x.TenantId, x.RunId, x.AttemptId },
                        principalSchema: "migration",
                        principalTable: "MigrationAttempts",
                        principalColumns: new[] { "TenantId", "RunId", "AttemptId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MigrationValidationResults_MigrationRuns_TenantId_RunId",
                        columns: x => new { x.TenantId, x.RunId },
                        principalSchema: "migration",
                        principalTable: "MigrationRuns",
                        principalColumns: new[] { "TenantId", "RunId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MigrationDryRunPreviewRows",
                schema: "migration",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviewId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StagedRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordType = table.Column<int>(type: "int", nullable: false),
                    Disposition = table.Column<int>(type: "int", nullable: false),
                    PlannedAction = table.Column<int>(type: "int", nullable: false),
                    Projection = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MigrationDryRunPreviewRows", x => new { x.TenantId, x.PreviewId, x.StagedRecordId });
                    table.ForeignKey(
                        name: "FK_MigrationDryRunPreviewRows_MigrationDryRunPreviews_TenantId_PreviewId",
                        columns: x => new { x.TenantId, x.PreviewId },
                        principalSchema: "migration",
                        principalTable: "MigrationDryRunPreviews",
                        principalColumns: new[] { "TenantId", "PreviewId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MigrationDryRunPreviewRows_MigrationStagedRecords_TenantId_RunId_StagedRecordId",
                        columns: x => new { x.TenantId, x.RunId, x.StagedRecordId },
                        principalSchema: "migration",
                        principalTable: "MigrationStagedRecords",
                        principalColumns: new[] { "TenantId", "RunId", "StagedRecordId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MigrationValidationRecords",
                schema: "migration",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StagedRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordType = table.Column<int>(type: "int", nullable: false),
                    Disposition = table.Column<int>(type: "int", nullable: false),
                    FindingCodesJson = table.Column<string>(type: "nvarchar(max)", maxLength: 32000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MigrationValidationRecords", x => new { x.TenantId, x.RunId, x.AttemptId, x.StagedRecordId });
                    table.ForeignKey(
                        name: "FK_MigrationValidationRecords_MigrationStagedRecords_TenantId_RunId_StagedRecordId",
                        columns: x => new { x.TenantId, x.RunId, x.StagedRecordId },
                        principalSchema: "migration",
                        principalTable: "MigrationStagedRecords",
                        principalColumns: new[] { "TenantId", "RunId", "StagedRecordId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MigrationValidationRecords_MigrationValidationResults_TenantId_RunId_AttemptId",
                        columns: x => new { x.TenantId, x.RunId, x.AttemptId },
                        principalSchema: "migration",
                        principalTable: "MigrationValidationResults",
                        principalColumns: new[] { "TenantId", "RunId", "AttemptId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MigrationDryRunPreviewRows_TenantId_RunId_StagedRecordId",
                schema: "migration",
                table: "MigrationDryRunPreviewRows",
                columns: new[] { "TenantId", "RunId", "StagedRecordId" });

            migrationBuilder.CreateIndex(
                name: "IX_MigrationDryRunPreviews_TenantId_RunId_AttemptId",
                schema: "migration",
                table: "MigrationDryRunPreviews",
                columns: new[] { "TenantId", "RunId", "AttemptId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MigrationDryRunPreviews_TenantId_RunId_ValidationAttemptId",
                schema: "migration",
                table: "MigrationDryRunPreviews",
                columns: new[] { "TenantId", "RunId", "ValidationAttemptId" });

            migrationBuilder.CreateIndex(
                name: "IX_MigrationStagedRecords_TenantId_RunId_PackageHash",
                schema: "migration",
                table: "MigrationStagedRecords",
                columns: new[] { "TenantId", "RunId", "PackageHash" });

            migrationBuilder.CreateIndex(
                name: "IX_MigrationStagedRecords_TenantId_RunId_SourceSequence",
                schema: "migration",
                table: "MigrationStagedRecords",
                columns: new[] { "TenantId", "RunId", "SourceSequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MigrationValidationFindings_TenantId_RunId_AttemptId",
                schema: "migration",
                table: "MigrationValidationFindings",
                columns: new[] { "TenantId", "RunId", "AttemptId" });

            migrationBuilder.CreateIndex(
                name: "IX_MigrationValidationRecords_TenantId_RunId_StagedRecordId",
                schema: "migration",
                table: "MigrationValidationRecords",
                columns: new[] { "TenantId", "RunId", "StagedRecordId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MigrationDryRunPreviewRows",
                schema: "migration");

            migrationBuilder.DropTable(
                name: "MigrationValidationFindings",
                schema: "migration");

            migrationBuilder.DropTable(
                name: "MigrationValidationRecords",
                schema: "migration");

            migrationBuilder.DropTable(
                name: "MigrationDryRunPreviews",
                schema: "migration");

            migrationBuilder.DropTable(
                name: "MigrationStagedRecords",
                schema: "migration");

            migrationBuilder.DropTable(
                name: "MigrationValidationResults",
                schema: "migration");
        }
    }
}
