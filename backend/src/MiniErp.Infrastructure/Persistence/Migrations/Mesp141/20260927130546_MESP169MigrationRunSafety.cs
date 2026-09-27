using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Mesp141
{
    /// <inheritdoc />
    public partial class MESP169MigrationRunSafety : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActionableMessage",
                schema: "migration",
                table: "MigrationValidationRecords",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CanonicalPayload",
                schema: "migration",
                table: "MigrationValidationRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorrectionOwner",
                schema: "migration",
                table: "MigrationValidationRecords",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ErrorClass",
                schema: "migration",
                table: "MigrationValidationRecords",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceRecordId",
                schema: "migration",
                table: "MigrationValidationRecords",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                schema: "migration",
                table: "MigrationRuns",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActionableMessage",
                schema: "migration",
                table: "MigrationValidationRecords");

            migrationBuilder.DropColumn(
                name: "CanonicalPayload",
                schema: "migration",
                table: "MigrationValidationRecords");

            migrationBuilder.DropColumn(
                name: "CorrectionOwner",
                schema: "migration",
                table: "MigrationValidationRecords");

            migrationBuilder.DropColumn(
                name: "ErrorClass",
                schema: "migration",
                table: "MigrationValidationRecords");

            migrationBuilder.DropColumn(
                name: "SourceRecordId",
                schema: "migration",
                table: "MigrationValidationRecords");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                schema: "migration",
                table: "MigrationRuns");
        }
    }
}
