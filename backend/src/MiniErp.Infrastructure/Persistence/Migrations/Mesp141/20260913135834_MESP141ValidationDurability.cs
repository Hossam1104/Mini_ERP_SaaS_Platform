using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Mesp141
{
    /// <inheritdoc />
    public partial class MESP141ValidationDurability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EvidenceConfirmed",
                schema: "migration",
                table: "MigrationRuns",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EvidenceConfirmed",
                schema: "migration",
                table: "MigrationAttempts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EvidenceConfirmed",
                schema: "migration",
                table: "MigrationIdempotency",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "AcceptedCount",
                schema: "migration",
                table: "MigrationValidationResults",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "QuarantinedCount",
                schema: "migration",
                table: "MigrationValidationResults",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RejectedCount",
                schema: "migration",
                table: "MigrationValidationResults",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalStagedRecords",
                schema: "migration",
                table: "MigrationValidationResults",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SourceSequence",
                schema: "migration",
                table: "MigrationValidationRecords",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AcceptedCount",
                schema: "migration",
                table: "MigrationDryRunPreviews",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "QuarantinedCount",
                schema: "migration",
                table: "MigrationDryRunPreviews",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RejectedCount",
                schema: "migration",
                table: "MigrationDryRunPreviews",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalStagedRecords",
                schema: "migration",
                table: "MigrationDryRunPreviews",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SourceSequence",
                schema: "migration",
                table: "MigrationDryRunPreviewRows",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EvidenceConfirmed",
                schema: "migration",
                table: "MigrationRuns");

            migrationBuilder.DropColumn(
                name: "EvidenceConfirmed",
                schema: "migration",
                table: "MigrationAttempts");

            migrationBuilder.DropColumn(
                name: "EvidenceConfirmed",
                schema: "migration",
                table: "MigrationIdempotency");

            migrationBuilder.DropColumn(
                name: "AcceptedCount",
                schema: "migration",
                table: "MigrationValidationResults");

            migrationBuilder.DropColumn(
                name: "QuarantinedCount",
                schema: "migration",
                table: "MigrationValidationResults");

            migrationBuilder.DropColumn(
                name: "RejectedCount",
                schema: "migration",
                table: "MigrationValidationResults");

            migrationBuilder.DropColumn(
                name: "TotalStagedRecords",
                schema: "migration",
                table: "MigrationValidationResults");

            migrationBuilder.DropColumn(
                name: "SourceSequence",
                schema: "migration",
                table: "MigrationValidationRecords");

            migrationBuilder.DropColumn(
                name: "AcceptedCount",
                schema: "migration",
                table: "MigrationDryRunPreviews");

            migrationBuilder.DropColumn(
                name: "QuarantinedCount",
                schema: "migration",
                table: "MigrationDryRunPreviews");

            migrationBuilder.DropColumn(
                name: "RejectedCount",
                schema: "migration",
                table: "MigrationDryRunPreviews");

            migrationBuilder.DropColumn(
                name: "TotalStagedRecords",
                schema: "migration",
                table: "MigrationDryRunPreviews");

            migrationBuilder.DropColumn(
                name: "SourceSequence",
                schema: "migration",
                table: "MigrationDryRunPreviewRows");
        }
    }
}
