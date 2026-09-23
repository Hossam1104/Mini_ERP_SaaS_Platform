using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Mesp141
{
    /// <inheritdoc />
    public partial class MESP141MultiCurrencyOpeningFxEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AppliedRate",
                schema: "migration",
                table: "MigrationEconomicRepresentations",
                type: "decimal(28,12)",
                precision: 28,
                scale: 12,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExchangeRateId",
                schema: "migration",
                table: "MigrationEconomicRepresentations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExchangeRateVersionId",
                schema: "migration",
                table: "MigrationEconomicRepresentations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExchangeRateVersionNumber",
                schema: "migration",
                table: "MigrationEconomicRepresentations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExpectedFunctionalCurrencyCode",
                schema: "migration",
                table: "MigrationEconomicRepresentations",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MonetaryPolicyId",
                schema: "migration",
                table: "MigrationEconomicRepresentations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MonetaryPolicyVersionNumber",
                schema: "migration",
                table: "MigrationEconomicRepresentations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "RateDate",
                schema: "migration",
                table: "MigrationEconomicRepresentations",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReportingAppliedRate",
                schema: "migration",
                table: "MigrationEconomicRepresentations",
                type: "decimal(28,12)",
                precision: 28,
                scale: 12,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReportingCurrencyCode",
                schema: "migration",
                table: "MigrationEconomicRepresentations",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReportingExchangeRateId",
                schema: "migration",
                table: "MigrationEconomicRepresentations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReportingExchangeRateVersionId",
                schema: "migration",
                table: "MigrationEconomicRepresentations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReportingExchangeRateVersionNumber",
                schema: "migration",
                table: "MigrationEconomicRepresentations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RoundingMode",
                schema: "migration",
                table: "MigrationEconomicRepresentations",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RoundingScale",
                schema: "migration",
                table: "MigrationEconomicRepresentations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TransactionAmount",
                schema: "migration",
                table: "MigrationEconomicRepresentations",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransactionCurrencyCode",
                schema: "migration",
                table: "MigrationEconomicRepresentations",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AppliedRate",
                schema: "migration",
                table: "MigrationEconomicRepresentations");

            migrationBuilder.DropColumn(
                name: "ExchangeRateId",
                schema: "migration",
                table: "MigrationEconomicRepresentations");

            migrationBuilder.DropColumn(
                name: "ExchangeRateVersionId",
                schema: "migration",
                table: "MigrationEconomicRepresentations");

            migrationBuilder.DropColumn(
                name: "ExchangeRateVersionNumber",
                schema: "migration",
                table: "MigrationEconomicRepresentations");

            migrationBuilder.DropColumn(
                name: "ExpectedFunctionalCurrencyCode",
                schema: "migration",
                table: "MigrationEconomicRepresentations");

            migrationBuilder.DropColumn(
                name: "MonetaryPolicyId",
                schema: "migration",
                table: "MigrationEconomicRepresentations");

            migrationBuilder.DropColumn(
                name: "MonetaryPolicyVersionNumber",
                schema: "migration",
                table: "MigrationEconomicRepresentations");

            migrationBuilder.DropColumn(
                name: "RateDate",
                schema: "migration",
                table: "MigrationEconomicRepresentations");

            migrationBuilder.DropColumn(
                name: "ReportingAppliedRate",
                schema: "migration",
                table: "MigrationEconomicRepresentations");

            migrationBuilder.DropColumn(
                name: "ReportingCurrencyCode",
                schema: "migration",
                table: "MigrationEconomicRepresentations");

            migrationBuilder.DropColumn(
                name: "ReportingExchangeRateId",
                schema: "migration",
                table: "MigrationEconomicRepresentations");

            migrationBuilder.DropColumn(
                name: "ReportingExchangeRateVersionId",
                schema: "migration",
                table: "MigrationEconomicRepresentations");

            migrationBuilder.DropColumn(
                name: "ReportingExchangeRateVersionNumber",
                schema: "migration",
                table: "MigrationEconomicRepresentations");

            migrationBuilder.DropColumn(
                name: "RoundingMode",
                schema: "migration",
                table: "MigrationEconomicRepresentations");

            migrationBuilder.DropColumn(
                name: "RoundingScale",
                schema: "migration",
                table: "MigrationEconomicRepresentations");

            migrationBuilder.DropColumn(
                name: "TransactionAmount",
                schema: "migration",
                table: "MigrationEconomicRepresentations");

            migrationBuilder.DropColumn(
                name: "TransactionCurrencyCode",
                schema: "migration",
                table: "MigrationEconomicRepresentations");
        }
    }
}
