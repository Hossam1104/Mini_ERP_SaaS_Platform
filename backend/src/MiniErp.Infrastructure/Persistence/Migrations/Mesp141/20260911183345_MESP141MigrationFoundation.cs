using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Mesp141
{
    /// <inheritdoc />
    public partial class MESP141MigrationFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "migration");

            migrationBuilder.CreateTable(
                name: "MigrationRuns",
                schema: "migration",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DefinitionId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DefinitionVersion = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SourceProfileId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SourceProfileVersion = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MigrationRuns", x => x.RunId);
                    table.UniqueConstraint("AK_MigrationRuns_TenantId_RunId", x => new { x.TenantId, x.RunId });
                });

            migrationBuilder.CreateTable(
                name: "MigrationAttempts",
                schema: "migration",
                columns: table => new
                {
                    AttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    PreviousAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Operation = table.Column<int>(type: "int", nullable: false),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RequestFingerprint = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SafeOutcomeCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MigrationAttempts", x => x.AttemptId);
                    table.ForeignKey(
                        name: "FK_MigrationAttempts_MigrationRuns_TenantId_RunId",
                        columns: x => new { x.TenantId, x.RunId },
                        principalSchema: "migration",
                        principalTable: "MigrationRuns",
                        principalColumns: new[] { "TenantId", "RunId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MigrationIdempotency",
                schema: "migration",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Operation = table.Column<int>(type: "int", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestFingerprint = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    AttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResultKind = table.Column<int>(type: "int", nullable: false),
                    ResultCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MigrationIdempotency", x => new { x.TenantId, x.Operation, x.IdempotencyKey });
                    table.ForeignKey(
                        name: "FK_MigrationIdempotency_MigrationRuns_TenantId_RunId",
                        columns: x => new { x.TenantId, x.RunId },
                        principalSchema: "migration",
                        principalTable: "MigrationRuns",
                        principalColumns: new[] { "TenantId", "RunId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MigrationAttempts_TenantId_AttemptId",
                schema: "migration",
                table: "MigrationAttempts",
                columns: new[] { "TenantId", "AttemptId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MigrationAttempts_TenantId_RunId_Sequence",
                schema: "migration",
                table: "MigrationAttempts",
                columns: new[] { "TenantId", "RunId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MigrationIdempotency_TenantId_RunId_Operation_IdempotencyKey",
                schema: "migration",
                table: "MigrationIdempotency",
                columns: new[] { "TenantId", "RunId", "Operation", "IdempotencyKey" });

            migrationBuilder.CreateIndex(
                name: "IX_MigrationRuns_TenantId_RunId",
                schema: "migration",
                table: "MigrationRuns",
                columns: new[] { "TenantId", "RunId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MigrationAttempts",
                schema: "migration");

            migrationBuilder.DropTable(
                name: "MigrationIdempotency",
                schema: "migration");

            migrationBuilder.DropTable(
                name: "MigrationRuns",
                schema: "migration");
        }
    }
}
