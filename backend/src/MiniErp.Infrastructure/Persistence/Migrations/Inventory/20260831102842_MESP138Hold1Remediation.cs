using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Inventory
{
    /// <inheritdoc />
    public partial class MESP138Hold1Remediation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AcknowledgementState",
                schema: "inventory",
                table: "CustomerReturns",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                schema: "inventory",
                table: "CustomerReturns",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CommitState",
                schema: "inventory",
                table: "CustomerReturns",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                schema: "inventory",
                table: "CustomerReturns",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DownstreamIdempotencyKey",
                schema: "inventory",
                table: "CustomerReturns",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EffectFingerprint",
                schema: "inventory",
                table: "CustomerReturns",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastAttemptAt",
                schema: "inventory",
                table: "CustomerReturns",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                schema: "inventory",
                table: "CustomerReturns",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReconciliationState",
                schema: "inventory",
                table: "CustomerReturns",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RequestFingerprint",
                schema: "inventory",
                table: "CustomerReturns",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "CommerciallyAcceptedQuantity",
                schema: "inventory",
                table: "CustomerReturnLines",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryMovementIdsJson",
                schema: "inventory",
                table: "CustomerReturnLines",
                type: "nvarchar(max)",
                maxLength: 8192,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MovementIdsJson",
                schema: "inventory",
                table: "CustomerReturnLines",
                type: "nvarchar(max)",
                maxLength: 8192,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "NonRestockableAcceptedQuantity",
                schema: "inventory",
                table: "CustomerReturnLines",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RejectedQuantity",
                schema: "inventory",
                table: "CustomerReturnLines",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestockedQuantity",
                schema: "inventory",
                table: "CustomerReturnLines",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReturns_TenantId_EffectFingerprint",
                schema: "inventory",
                table: "CustomerReturns",
                columns: new[] { "TenantId", "EffectFingerprint" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CustomerReturns_TenantId_EffectFingerprint",
                schema: "inventory",
                table: "CustomerReturns");

            migrationBuilder.DropColumn(
                name: "AcknowledgementState",
                schema: "inventory",
                table: "CustomerReturns");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                schema: "inventory",
                table: "CustomerReturns");

            migrationBuilder.DropColumn(
                name: "CommitState",
                schema: "inventory",
                table: "CustomerReturns");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                schema: "inventory",
                table: "CustomerReturns");

            migrationBuilder.DropColumn(
                name: "DownstreamIdempotencyKey",
                schema: "inventory",
                table: "CustomerReturns");

            migrationBuilder.DropColumn(
                name: "EffectFingerprint",
                schema: "inventory",
                table: "CustomerReturns");

            migrationBuilder.DropColumn(
                name: "LastAttemptAt",
                schema: "inventory",
                table: "CustomerReturns");

            migrationBuilder.DropColumn(
                name: "LastError",
                schema: "inventory",
                table: "CustomerReturns");

            migrationBuilder.DropColumn(
                name: "ReconciliationState",
                schema: "inventory",
                table: "CustomerReturns");

            migrationBuilder.DropColumn(
                name: "RequestFingerprint",
                schema: "inventory",
                table: "CustomerReturns");

            migrationBuilder.DropColumn(
                name: "CommerciallyAcceptedQuantity",
                schema: "inventory",
                table: "CustomerReturnLines");

            migrationBuilder.DropColumn(
                name: "DeliveryMovementIdsJson",
                schema: "inventory",
                table: "CustomerReturnLines");

            migrationBuilder.DropColumn(
                name: "MovementIdsJson",
                schema: "inventory",
                table: "CustomerReturnLines");

            migrationBuilder.DropColumn(
                name: "NonRestockableAcceptedQuantity",
                schema: "inventory",
                table: "CustomerReturnLines");

            migrationBuilder.DropColumn(
                name: "RejectedQuantity",
                schema: "inventory",
                table: "CustomerReturnLines");

            migrationBuilder.DropColumn(
                name: "RestockedQuantity",
                schema: "inventory",
                table: "CustomerReturnLines");
        }
    }
}
