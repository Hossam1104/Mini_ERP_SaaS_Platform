using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Mesp141
{
    /// <inheritdoc />
    public partial class Mesp141Slice11Reconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MigrationReconciliations",
                schema: "migration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    EvidenceFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CalculatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SubmittedCount = table.Column<int>(type: "int", nullable: false),
                    AcceptedCount = table.Column<int>(type: "int", nullable: false),
                    RejectedCount = table.Column<int>(type: "int", nullable: false),
                    DuplicateCount = table.Column<int>(type: "int", nullable: false),
                    SkippedCount = table.Column<int>(type: "int", nullable: false),
                    QuarantinedCount = table.Column<int>(type: "int", nullable: false),
                    UnresolvedCount = table.Column<int>(type: "int", nullable: false),
                    RequiredApprovalCount = table.Column<int>(type: "int", nullable: false),
                    SourceDebit = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    SourceCredit = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    TargetDebit = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    TargetCredit = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    Variance = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    ApprovalPolicyId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ApprovalPolicyVersion = table.Column<int>(type: "int", nullable: true),
                    ApprovalPolicyCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ApprovalPolicyEffectiveFrom = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovalPolicyEffectiveTo = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovalEnforcesSeparationOfDuties = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MigrationReconciliations", x => x.Id);
                    table.UniqueConstraint("AK_MigrationReconciliations_TenantId_RunId_Id", x => new { x.TenantId, x.RunId, x.Id });
                    table.ForeignKey(
                        name: "FK_MigrationReconciliations_MigrationAttempts_TenantId_RunId_AttemptId",
                        columns: x => new { x.TenantId, x.RunId, x.AttemptId },
                        principalSchema: "migration",
                        principalTable: "MigrationAttempts",
                        principalColumns: new[] { "TenantId", "RunId", "AttemptId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MigrationReconciliations_MigrationRuns_TenantId_RunId",
                        columns: x => new { x.TenantId, x.RunId },
                        principalSchema: "migration",
                        principalTable: "MigrationRuns",
                        principalColumns: new[] { "TenantId", "RunId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MigrationHandoverReadiness",
                schema: "migration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReconciliationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReconciliationVersion = table.Column<int>(type: "int", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvidenceFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    BusinessReady = table.Column<bool>(type: "bit", nullable: false),
                    ProductionReady = table.Column<bool>(type: "bit", nullable: false),
                    Mesp48Complete = table.Column<bool>(type: "bit", nullable: false),
                    Mesp50Complete = table.Column<bool>(type: "bit", nullable: false),
                    TenantActivationPerformed = table.Column<bool>(type: "bit", nullable: false),
                    ResultCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MigrationHandoverReadiness", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MigrationHandoverReadiness_MigrationAttempts_TenantId_RunId_AttemptId",
                        columns: x => new { x.TenantId, x.RunId, x.AttemptId },
                        principalSchema: "migration",
                        principalTable: "MigrationAttempts",
                        principalColumns: new[] { "TenantId", "RunId", "AttemptId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MigrationHandoverReadiness_MigrationReconciliations_TenantId_RunId_ReconciliationId",
                        columns: x => new { x.TenantId, x.RunId, x.ReconciliationId },
                        principalSchema: "migration",
                        principalTable: "MigrationReconciliations",
                        principalColumns: new[] { "TenantId", "RunId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MigrationReconciliationApprovals",
                schema: "migration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReconciliationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReconciliationVersion = table.Column<int>(type: "int", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvidenceFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Domain = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RequirementKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PolicyId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PolicyVersion = table.Column<int>(type: "int", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Decision = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    EvidenceConfirmed = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MigrationReconciliationApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MigrationReconciliationApprovals_MigrationAttempts_TenantId_RunId_AttemptId",
                        columns: x => new { x.TenantId, x.RunId, x.AttemptId },
                        principalSchema: "migration",
                        principalTable: "MigrationAttempts",
                        principalColumns: new[] { "TenantId", "RunId", "AttemptId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MigrationReconciliationApprovals_MigrationReconciliations_TenantId_RunId_ReconciliationId",
                        columns: x => new { x.TenantId, x.RunId, x.ReconciliationId },
                        principalSchema: "migration",
                        principalTable: "MigrationReconciliations",
                        principalColumns: new[] { "TenantId", "RunId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MigrationReconciliationDetails",
                schema: "migration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReconciliationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Domain = table.Column<int>(type: "int", nullable: false),
                    ScopeKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OpeningDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CurrencyCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    TransactionCurrencyCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    FunctionalCurrencyCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    SourceCount = table.Column<int>(type: "int", nullable: false),
                    SourceDebit = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    SourceCredit = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    TargetDebit = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    TargetCredit = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    Variance = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    SourceAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    TargetAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    AmountVariance = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    OwnerRoundingDifference = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    TransactionAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    FunctionalAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    SubsidiaryEstablishedAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    GlControlAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    ExchangeRateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExchangeRateVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExchangeRateVersionNumber = table.Column<int>(type: "int", nullable: true),
                    AppliedRate = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    SourceQuantity = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    TargetQuantity = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    QuantityVariance = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    ControlAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PostingRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PostingRuleVersionNumber = table.Column<int>(type: "int", nullable: true),
                    OwnerSourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UnitOfMeasureId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceContract = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SourceEvent = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    RoundingPolicyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RoundingPolicyVersionNumber = table.Column<int>(type: "int", nullable: true),
                    RoundingScale = table.Column<int>(type: "int", nullable: true),
                    RoundingMode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    IsBlocking = table.Column<bool>(type: "bit", nullable: false),
                    FindingCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Explanation = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    EffectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OwnerReferenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LinkedAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MigrationReconciliationDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MigrationReconciliationDetails_MigrationReconciliations_TenantId_RunId_ReconciliationId",
                        columns: x => new { x.TenantId, x.RunId, x.ReconciliationId },
                        principalSchema: "migration",
                        principalTable: "MigrationReconciliations",
                        principalColumns: new[] { "TenantId", "RunId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MigrationReconciliationRequirements",
                schema: "migration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReconciliationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Domain = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RequirementKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RequiredCount = table.Column<int>(type: "int", nullable: false),
                    PolicyId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PolicyVersion = table.Column<int>(type: "int", nullable: false),
                    EnforceSeparationOfDuties = table.Column<bool>(type: "bit", nullable: false),
                    EligibleActorIdsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MigrationReconciliationRequirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MigrationReconciliationRequirements_MigrationReconciliations_TenantId_RunId_ReconciliationId",
                        columns: x => new { x.TenantId, x.RunId, x.ReconciliationId },
                        principalSchema: "migration",
                        principalTable: "MigrationReconciliations",
                        principalColumns: new[] { "TenantId", "RunId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MigrationHandoverReadiness_TenantId_ReconciliationId_ReconciliationVersion",
                schema: "migration",
                table: "MigrationHandoverReadiness",
                columns: new[] { "TenantId", "ReconciliationId", "ReconciliationVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MigrationHandoverReadiness_TenantId_RunId_AttemptId",
                schema: "migration",
                table: "MigrationHandoverReadiness",
                columns: new[] { "TenantId", "RunId", "AttemptId" });

            migrationBuilder.CreateIndex(
                name: "IX_MigrationHandoverReadiness_TenantId_RunId_IdempotencyKey",
                schema: "migration",
                table: "MigrationHandoverReadiness",
                columns: new[] { "TenantId", "RunId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MigrationHandoverReadiness_TenantId_RunId_ReconciliationId",
                schema: "migration",
                table: "MigrationHandoverReadiness",
                columns: new[] { "TenantId", "RunId", "ReconciliationId" });

            migrationBuilder.CreateIndex(
                name: "IX_MigrationReconciliationApprovals_TenantId_ReconciliationId_ReconciliationVersion_Domain_RequirementKey_ActorId",
                schema: "migration",
                table: "MigrationReconciliationApprovals",
                columns: new[] { "TenantId", "ReconciliationId", "ReconciliationVersion", "Domain", "RequirementKey", "ActorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MigrationReconciliationApprovals_TenantId_RunId_AttemptId",
                schema: "migration",
                table: "MigrationReconciliationApprovals",
                columns: new[] { "TenantId", "RunId", "AttemptId" });

            migrationBuilder.CreateIndex(
                name: "IX_MigrationReconciliationApprovals_TenantId_RunId_IdempotencyKey",
                schema: "migration",
                table: "MigrationReconciliationApprovals",
                columns: new[] { "TenantId", "RunId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MigrationReconciliationApprovals_TenantId_RunId_ReconciliationId",
                schema: "migration",
                table: "MigrationReconciliationApprovals",
                columns: new[] { "TenantId", "RunId", "ReconciliationId" });

            migrationBuilder.CreateIndex(
                name: "IX_MigrationReconciliationDetails_TenantId_ReconciliationId_Domain_ScopeKey",
                schema: "migration",
                table: "MigrationReconciliationDetails",
                columns: new[] { "TenantId", "ReconciliationId", "Domain", "ScopeKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MigrationReconciliationDetails_TenantId_RunId_ReconciliationId",
                schema: "migration",
                table: "MigrationReconciliationDetails",
                columns: new[] { "TenantId", "RunId", "ReconciliationId" });

            migrationBuilder.CreateIndex(
                name: "IX_MigrationReconciliationRequirements_TenantId_ReconciliationId_Domain_RequirementKey",
                schema: "migration",
                table: "MigrationReconciliationRequirements",
                columns: new[] { "TenantId", "ReconciliationId", "Domain", "RequirementKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MigrationReconciliationRequirements_TenantId_RunId_ReconciliationId",
                schema: "migration",
                table: "MigrationReconciliationRequirements",
                columns: new[] { "TenantId", "RunId", "ReconciliationId" });

            migrationBuilder.CreateIndex(
                name: "IX_MigrationReconciliations_TenantId_RunId_AttemptId",
                schema: "migration",
                table: "MigrationReconciliations",
                columns: new[] { "TenantId", "RunId", "AttemptId" });

            migrationBuilder.CreateIndex(
                name: "IX_MigrationReconciliations_TenantId_RunId_EvidenceFingerprint",
                schema: "migration",
                table: "MigrationReconciliations",
                columns: new[] { "TenantId", "RunId", "EvidenceFingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MigrationReconciliations_TenantId_RunId_IdempotencyKey",
                schema: "migration",
                table: "MigrationReconciliations",
                columns: new[] { "TenantId", "RunId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MigrationReconciliations_TenantId_RunId_VersionNumber",
                schema: "migration",
                table: "MigrationReconciliations",
                columns: new[] { "TenantId", "RunId", "VersionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MigrationHandoverReadiness",
                schema: "migration");

            migrationBuilder.DropTable(
                name: "MigrationReconciliationApprovals",
                schema: "migration");

            migrationBuilder.DropTable(
                name: "MigrationReconciliationDetails",
                schema: "migration");

            migrationBuilder.DropTable(
                name: "MigrationReconciliationRequirements",
                schema: "migration");

            migrationBuilder.DropTable(
                name: "MigrationReconciliations",
                schema: "migration");
        }
    }
}
