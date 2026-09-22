using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Mesp141
{
    /// <inheritdoc />
    public partial class MESP141MigrationOpeningExpectations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OwnerSourceId",
                schema: "migration",
                table: "MigrationEconomicRepresentations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ControlAccountId",
                schema: "migration",
                table: "MigrationEconomicRepresentations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FunctionalAmount",
                schema: "migration",
                table: "MigrationEconomicRepresentations",
                type: "decimal(19,8)",
                precision: 19,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OffsetAccountId",
                schema: "migration",
                table: "MigrationEconomicRepresentations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PostingRuleId",
                schema: "migration",
                table: "MigrationEconomicRepresentations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PostingRuleVersionNumber",
                schema: "migration",
                table: "MigrationEconomicRepresentations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Reversal",
                schema: "migration",
                table: "MigrationEconomicRepresentations",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceContract",
                schema: "migration",
                table: "MigrationEconomicRepresentations",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceEvent",
                schema: "migration",
                table: "MigrationEconomicRepresentations",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceEvidenceId",
                schema: "migration",
                table: "MigrationEconomicRepresentations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceEvidenceVersion",
                schema: "migration",
                table: "MigrationEconomicRepresentations",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OwnerSourceId",
                schema: "migration",
                table: "MigrationEconomicRepresentations");

            migrationBuilder.DropColumn(
                name: "ControlAccountId",
                schema: "migration",
                table: "MigrationEconomicRepresentations");

            migrationBuilder.DropColumn(
                name: "FunctionalAmount",
                schema: "migration",
                table: "MigrationEconomicRepresentations");

            migrationBuilder.DropColumn(
                name: "OffsetAccountId",
                schema: "migration",
                table: "MigrationEconomicRepresentations");

            migrationBuilder.DropColumn(
                name: "PostingRuleId",
                schema: "migration",
                table: "MigrationEconomicRepresentations");

            migrationBuilder.DropColumn(
                name: "PostingRuleVersionNumber",
                schema: "migration",
                table: "MigrationEconomicRepresentations");

            migrationBuilder.DropColumn(
                name: "Reversal",
                schema: "migration",
                table: "MigrationEconomicRepresentations");

            migrationBuilder.DropColumn(
                name: "SourceContract",
                schema: "migration",
                table: "MigrationEconomicRepresentations");

            migrationBuilder.DropColumn(
                name: "SourceEvent",
                schema: "migration",
                table: "MigrationEconomicRepresentations");

            migrationBuilder.DropColumn(
                name: "SourceEvidenceId",
                schema: "migration",
                table: "MigrationEconomicRepresentations");

            migrationBuilder.DropColumn(
                name: "SourceEvidenceVersion",
                schema: "migration",
                table: "MigrationEconomicRepresentations");
        }
    }
}
