using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Audit;
using MiniErp.App.Modules.Migration;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.Migration;
using MiniErp.Infrastructure.Persistence.Modules.Migration;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class MigrationFoundationTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid Actor = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Session = Guid.Parse("44444444-4444-4444-4444-444444444444");

    /// <summary>
    /// The authoritative MESP-40 BRD section 7.2 lifecycle, expressed once as
    /// data. Every ordered pair of states not listed here must be refused, so
    /// the all-pairs test below also protects against a permissive edge being
    /// added silently later.
    /// </summary>
    private static readonly Dictionary<MigrationRunStatus, MigrationRunStatus[]> ExpectedLifecycle = new()
    {
        [MigrationRunStatus.Draft] = [MigrationRunStatus.Prepared, MigrationRunStatus.Cancelled],
        [MigrationRunStatus.Prepared] = [MigrationRunStatus.Validating, MigrationRunStatus.Cancelled],
        [MigrationRunStatus.Validating] =
        [
            MigrationRunStatus.ValidationFailed,
            MigrationRunStatus.Validated,
            MigrationRunStatus.Cancelled
        ],
        [MigrationRunStatus.ValidationFailed] =
        [
            MigrationRunStatus.Corrected,
            MigrationRunStatus.Prepared,
            MigrationRunStatus.Cancelled
        ],
        [MigrationRunStatus.Validated] =
        [
            MigrationRunStatus.AwaitingApproval,
            MigrationRunStatus.Approved,
            MigrationRunStatus.Cancelled
        ],
        [MigrationRunStatus.AwaitingApproval] = [MigrationRunStatus.Approved, MigrationRunStatus.Cancelled],
        [MigrationRunStatus.Approved] = [MigrationRunStatus.Executing, MigrationRunStatus.Cancelled],
        [MigrationRunStatus.Executing] =
        [
            MigrationRunStatus.Completed,
            MigrationRunStatus.PartiallyCompleted,
            MigrationRunStatus.Failed,
            MigrationRunStatus.OutcomeUnknown
        ],
        [MigrationRunStatus.Completed] = [MigrationRunStatus.ReconciliationPending],
        [MigrationRunStatus.PartiallyCompleted] = [MigrationRunStatus.ReconciliationPending],
        [MigrationRunStatus.Failed] =
        [
            MigrationRunStatus.Corrected,
            MigrationRunStatus.Prepared,
            MigrationRunStatus.OutcomeUnknown,
            MigrationRunStatus.ReconciliationPending
        ],
        [MigrationRunStatus.OutcomeUnknown] = [MigrationRunStatus.ReconciliationPending],
        [MigrationRunStatus.ReconciliationPending] = [MigrationRunStatus.Reconciled, MigrationRunStatus.Corrected],
        [MigrationRunStatus.Reconciled] = [MigrationRunStatus.ReadyForHandover],
        [MigrationRunStatus.ReadyForHandover] = [MigrationRunStatus.Closed],
        [MigrationRunStatus.Corrected] = [MigrationRunStatus.Prepared],
        [MigrationRunStatus.Cancelled] = [],
        [MigrationRunStatus.Closed] = []
    };

    // ---------------------------------------------------------------- F1 ----

    [Fact]
    public void Transition_map_permits_exactly_the_authoritative_lifecycle_for_every_state_pair()
    {
        var states = Enum.GetValues<MigrationRunStatus>();

        // Every state in the persisted vocabulary must be mapped, so no state
        // can be reached and then silently have undefined behavior.
        Assert.Equal(states.Length, ExpectedLifecycle.Count);

        foreach (var from in states)
        {
            foreach (var to in states)
            {
                var expected = ExpectedLifecycle[from].Contains(to);
                Assert.Equal(expected, MigrationRun.IsTransitionAllowed(from, to));
            }
        }
    }

    [Theory]
    // Execution must not jump the approval gate (WF-03 entry criteria).
    [InlineData(MigrationRunStatus.Validated, MigrationRunStatus.Executing)]
    // "Cancelled is a terminal PRE-COMMIT outcome" (section 7.2). A run at or
    // past the effect boundary must never claim it had no effect.
    [InlineData(MigrationRunStatus.Executing, MigrationRunStatus.Cancelled)]
    [InlineData(MigrationRunStatus.Completed, MigrationRunStatus.Cancelled)]
    [InlineData(MigrationRunStatus.PartiallyCompleted, MigrationRunStatus.Cancelled)]
    [InlineData(MigrationRunStatus.Failed, MigrationRunStatus.Cancelled)]
    [InlineData(MigrationRunStatus.OutcomeUnknown, MigrationRunStatus.Cancelled)]
    [InlineData(MigrationRunStatus.ReconciliationPending, MigrationRunStatus.Cancelled)]
    // An unprovable outcome requires reconciliation and is never automatically
    // replayed or corrected away (section 13.7, M40-AC-022).
    [InlineData(MigrationRunStatus.OutcomeUnknown, MigrationRunStatus.Corrected)]
    [InlineData(MigrationRunStatus.OutcomeUnknown, MigrationRunStatus.Prepared)]
    [InlineData(MigrationRunStatus.OutcomeUnknown, MigrationRunStatus.Executing)]
    // Validation crosses no effect boundary, so it cannot produce the
    // post-effect "cannot be proved" outcome (M40-RULE-017, M40-AC-014).
    [InlineData(MigrationRunStatus.Validating, MigrationRunStatus.OutcomeUnknown)]
    // Execution outcomes route through reconciliation before handover.
    [InlineData(MigrationRunStatus.Completed, MigrationRunStatus.ReadyForHandover)]
    [InlineData(MigrationRunStatus.Completed, MigrationRunStatus.Closed)]
    [InlineData(MigrationRunStatus.ReconciliationPending, MigrationRunStatus.ReadyForHandover)]
    // Genuinely terminal states go nowhere.
    [InlineData(MigrationRunStatus.Cancelled, MigrationRunStatus.Prepared)]
    [InlineData(MigrationRunStatus.Closed, MigrationRunStatus.ReconciliationPending)]
    public void Unsafe_lifecycle_transitions_are_refused(MigrationRunStatus from, MigrationRunStatus to) =>
        Assert.False(MigrationRun.IsTransitionAllowed(from, to));

    [Fact]
    public void Unknown_outcome_reaches_closure_only_through_reconciliation()
    {
        var run = Draft();

        Assert.True(run.TryTransition(MigrationRunStatus.Prepared).Allowed);
        Assert.True(run.TryTransition(MigrationRunStatus.Validating).Allowed);
        Assert.True(run.TryTransition(MigrationRunStatus.Validated).Allowed);
        Assert.True(run.TryTransition(MigrationRunStatus.Approved).Allowed);
        Assert.True(run.TryTransition(MigrationRunStatus.Executing).Allowed);
        Assert.True(run.TryTransition(MigrationRunStatus.OutcomeUnknown).Allowed);

        Assert.True(run.RequiresReconciliation);
        Assert.True(run.HasReachedEffectBoundary);
        Assert.False(run.IsTerminal);

        Assert.True(run.TryTransition(MigrationRunStatus.ReconciliationPending).Allowed);
        Assert.True(run.TryTransition(MigrationRunStatus.Reconciled).Allowed);
        Assert.True(run.TryTransition(MigrationRunStatus.ReadyForHandover).Allowed);
        Assert.True(run.TryTransition(MigrationRunStatus.Closed).Allowed);
        Assert.True(run.IsTerminal);
    }

    [Fact]
    public void Only_cancelled_and_closed_are_terminal_and_completed_is_not()
    {
        Assert.Equal(
            new[] { MigrationRunStatus.Cancelled, MigrationRunStatus.Closed },
            MigrationRun.TerminalStates.OrderBy(state => state).ToArray());
        Assert.DoesNotContain(MigrationRunStatus.Completed, MigrationRun.TerminalStates);
        Assert.Contains(MigrationRunStatus.Completed, MigrationRun.EffectBoundaryStates);
        Assert.Contains(MigrationRunStatus.OutcomeUnknown, MigrationRun.ReconciliationRequiredStates);
        Assert.Contains(MigrationRunStatus.ReconciliationPending, MigrationRun.ReconciliationRequiredStates);
    }

    [Fact]
    public void No_state_at_or_after_the_effect_boundary_can_be_cancelled() =>
        Assert.DoesNotContain(
            MigrationRun.EffectBoundaryStates,
            state => MigrationRun.IsTransitionAllowed(state, MigrationRunStatus.Cancelled));

    [Fact]
    public void Undefined_status_values_fail_closed()
    {
        Assert.False(MigrationRun.IsTransitionAllowed((MigrationRunStatus)9999, MigrationRunStatus.Prepared));
        Assert.False(MigrationRun.IsTransitionAllowed(MigrationRunStatus.Draft, (MigrationRunStatus)9999));
        Assert.False(MigrationRun.IsTransitionAllowed(default, MigrationRunStatus.Prepared));
    }

    [Theory]
    [InlineData(MigrationRunStatus.Draft, MigrationOperationKind.Validation, false)]
    [InlineData(MigrationRunStatus.Prepared, MigrationOperationKind.Validation, true)]
    [InlineData(MigrationRunStatus.Validating, MigrationOperationKind.Validation, true)]
    [InlineData(MigrationRunStatus.Prepared, MigrationOperationKind.Execution, false)]
    [InlineData(MigrationRunStatus.Validated, MigrationOperationKind.Execution, false)]
    [InlineData(MigrationRunStatus.Approved, MigrationOperationKind.Execution, true)]
    [InlineData(MigrationRunStatus.Executing, MigrationOperationKind.Execution, true)]
    [InlineData(MigrationRunStatus.Validated, MigrationOperationKind.DryRun, true)]
    [InlineData(MigrationRunStatus.OutcomeUnknown, MigrationOperationKind.Execution, false)]
    [InlineData(MigrationRunStatus.ReconciliationPending, MigrationOperationKind.Execution, false)]
    [InlineData(MigrationRunStatus.Cancelled, MigrationOperationKind.Validation, false)]
    [InlineData(MigrationRunStatus.Closed, MigrationOperationKind.Execution, false)]
    public void Attempts_are_permitted_only_by_the_persisted_run_state(
        MigrationRunStatus status,
        MigrationOperationKind operation,
        bool expected) =>
        Assert.Equal(expected, RunAt(status).PermitsAttempt(operation));

    [Fact]
    public void Run_identity_and_creation_time_cannot_be_supplied_by_a_caller()
    {
        // The only public creation path derives both from the server. Two runs
        // created from the same inputs therefore never share an identity.
        var first = Draft();
        var second = Draft();

        Assert.NotEqual(first.RunId, second.RunId);
        Assert.NotEqual(Guid.Empty, first.RunId);
        Assert.DoesNotContain(
            typeof(MigrationRun).GetMethod(nameof(MigrationRun.Create))!.GetParameters(),
            parameter => parameter.Name is "runId" or "createdAt");
    }

    // ---------------------------------------------------------------- F2 ----

    [Fact]
    public async Task Attempt_sequence_and_predecessor_are_derived_from_persisted_lineage()
    {
        await using var fixture = await MigrationFixture.CreateAsync();
        var run = await fixture.PreparedRunAsync("lineage-run", MigrationOperationKind.Validation);

        var first = await fixture.Service.StartAttemptAsync(
            fixture.Request, run.RunId, MigrationOperationKind.Validation, "lineage-1", "fp-1");
        Assert.True(first.Succeeded, first.Code);
        Assert.Equal(1, first.Value!.Sequence);
        Assert.Null(first.Value.PreviousAttemptId);

        await fixture.FinishAttemptAsync(run.RunId, first.Value, MigrationAttemptOutcome.KnownFailure);

        var second = await fixture.Service.StartAttemptAsync(
            fixture.Request, run.RunId, MigrationOperationKind.Validation, "lineage-2", "fp-2");
        Assert.True(second.Succeeded, second.Code);
        Assert.Equal(2, second.Value!.Sequence);
        Assert.Equal(first.Value.AttemptId, second.Value.PreviousAttemptId);

        // The command surface carries no lineage inputs at all, so a caller
        // cannot forge, skip or reuse a sequence or predecessor.
        var commandParameters = typeof(StartMigrationAttemptCommand)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(parameter => parameter.Name)
            .ToArray();
        Assert.DoesNotContain("Sequence", commandParameters);
        Assert.DoesNotContain("PreviousAttemptId", commandParameters);
        Assert.DoesNotContain("AttemptId", commandParameters);
        Assert.DoesNotContain("StartedAt", commandParameters);
    }

    [Fact]
    public async Task A_second_attempt_is_refused_while_the_previous_attempt_is_still_open()
    {
        await using var fixture = await MigrationFixture.CreateAsync();
        var run = await fixture.PreparedRunAsync("open-run", MigrationOperationKind.Validation);

        var first = await fixture.Service.StartAttemptAsync(
            fixture.Request, run.RunId, MigrationOperationKind.Validation, "open-1", "fp-1");
        Assert.True(first.Succeeded, first.Code);

        var second = await fixture.Service.StartAttemptAsync(
            fixture.Request, run.RunId, MigrationOperationKind.Validation, "open-2", "fp-2");

        Assert.Equal(MigrationResultKind.Rejected, second.Kind);
        Assert.Equal("migration_attempt_previous_still_open", second.Code);
    }

    [Fact]
    public async Task A_retry_that_reads_its_own_concurrently_committed_attempt_replays_instead_of_being_refused()
    {
        // Found by the SQL Server attempt race: a caller missed the idempotency
        // row, then saw the winner's open attempt and refused its own retry as
        // "previous still open". The interceptor commits the winner exactly
        // between those two reads, so the interleaving is forced, not hoped for.
        var race = new CommitBeforeAttemptsReadInterceptor();
        await using var fixture = await MigrationFixture.CreateFileBackedAsync(race);
        var run = await fixture.PreparedRunAsync("skew-run", MigrationOperationKind.Validation);
        race.Arm(() => fixture.Service.StartAttemptAsync(
            fixture.Request, run.RunId, MigrationOperationKind.Validation, "skew-key", "skew-fp"));

        var retry = await fixture.Service.StartAttemptAsync(
            fixture.Request, run.RunId, MigrationOperationKind.Validation, "skew-key", "skew-fp");

        Assert.True(race.WinnerResult?.Succeeded, race.WinnerResult?.Code);
        Assert.Equal(MigrationResultKind.Replayed, retry.Kind);
        Assert.Equal(race.WinnerResult!.Value!.AttemptId, retry.Value!.AttemptId);
        Assert.Single(await fixture.Persistence.ListAttemptsAsync(fixture.Tenant, run.RunId));
    }

    [Fact]
    public async Task A_different_request_that_reads_a_concurrently_committed_open_attempt_is_still_refused()
    {
        // The replay recovery above must not dissolve the lineage guard: a
        // different key that loses the same interleaving is a genuine refusal.
        var race = new CommitBeforeAttemptsReadInterceptor();
        await using var fixture = await MigrationFixture.CreateFileBackedAsync(race);
        var run = await fixture.PreparedRunAsync("skew-guard-run", MigrationOperationKind.Validation);
        race.Arm(() => fixture.Service.StartAttemptAsync(
            fixture.Request, run.RunId, MigrationOperationKind.Validation, "skew-winner", "skew-winner-fp"));

        var other = await fixture.Service.StartAttemptAsync(
            fixture.Request, run.RunId, MigrationOperationKind.Validation, "skew-other", "skew-other-fp");

        Assert.True(race.WinnerResult?.Succeeded, race.WinnerResult?.Code);
        Assert.Equal(MigrationResultKind.Rejected, other.Kind);
        Assert.Equal("migration_attempt_previous_still_open", other.Code);
        Assert.Single(await fixture.Persistence.ListAttemptsAsync(fixture.Tenant, run.RunId));
    }

    [Fact]
    public async Task A_second_attempt_is_refused_after_an_unprovable_outcome()
    {
        await using var fixture = await MigrationFixture.CreateAsync();
        var run = await fixture.PreparedRunAsync("unknown-run", MigrationOperationKind.Validation);

        var first = await fixture.Service.StartAttemptAsync(
            fixture.Request, run.RunId, MigrationOperationKind.Validation, "unknown-1", "fp-1");
        await fixture.FinishAttemptAsync(run.RunId, first.Value!, MigrationAttemptOutcome.UnknownOutcome);

        var second = await fixture.Service.StartAttemptAsync(
            fixture.Request, run.RunId, MigrationOperationKind.Validation, "unknown-2", "fp-2");

        Assert.Equal(MigrationResultKind.Rejected, second.Kind);
        Assert.Equal("migration_attempt_previous_outcome_unknown", second.Code);
    }

    [Fact]
    public async Task An_attempt_is_refused_when_the_persisted_run_state_does_not_permit_the_operation()
    {
        await using var fixture = await MigrationFixture.CreateAsync();
        var run = await fixture.PreparedRunAsync("gate-run", MigrationOperationKind.Validation);

        // The run is Prepared, so execution has not passed the approval gate.
        var executed = await fixture.Service.StartAttemptAsync(
            fixture.Request, run.RunId, MigrationOperationKind.Execution, "gate-1", "fp-1");

        Assert.Equal(MigrationResultKind.Rejected, executed.Kind);
        Assert.Equal("migration_operation_not_permitted_by_run_state", executed.Code);
    }

    [Fact]
    public async Task An_attempt_is_refused_on_a_terminal_run()
    {
        await using var fixture = await MigrationFixture.CreateAsync();
        var created = await fixture.CreateRunAsync("terminal-run", MigrationOperationKind.Validation);
        var cancelled = await fixture.TransitionAsync(created, MigrationRunStatus.Cancelled);

        var attempt = await fixture.Service.StartAttemptAsync(
            fixture.Request, cancelled.RunId, MigrationOperationKind.Validation, "terminal-1", "fp-1");

        Assert.Equal(MigrationResultKind.Rejected, attempt.Kind);
        Assert.Equal("migration_run_terminal", attempt.Code);
    }

    [Fact]
    public async Task A_persisted_run_cannot_be_moved_outside_the_transition_map()
    {
        await using var fixture = await MigrationFixture.CreateAsync();
        var created = await fixture.CreateRunAsync("map-run", MigrationOperationKind.Validation);

        var refused = await fixture.Service.TransitionRunAsync(
            fixture.Request, created.RunId, MigrationRunStatus.Completed, created.Version);

        Assert.Equal(MigrationResultKind.Rejected, refused.Kind);
        Assert.Equal("invalid_state_transition", refused.Code);

        var unchanged = await fixture.Service.FindRunAsync(fixture.Tenant, created.RunId);
        Assert.Equal(MigrationRunStatus.Draft, unchanged.Value!.Status);
    }

    [Fact]
    public async Task A_stale_version_cannot_apply_a_run_transition()
    {
        await using var fixture = await MigrationFixture.CreateAsync();
        var created = await fixture.CreateRunAsync("version-run", MigrationOperationKind.Validation);
        var staleVersion = created.Version;

        var applied = await fixture.TransitionAsync(created, MigrationRunStatus.Prepared);
        Assert.Equal(MigrationRunStatus.Prepared, applied.Status);

        var stale = await fixture.Service.TransitionRunAsync(
            fixture.Request, created.RunId, MigrationRunStatus.Validating, staleVersion);

        Assert.Equal(MigrationResultKind.Rejected, stale.Kind);
        Assert.Equal("migration_run_version_conflict", stale.Code);
    }

    [Fact]
    public async Task An_attempt_outcome_is_recorded_once_and_never_rewritten()
    {
        await using var fixture = await MigrationFixture.CreateAsync();
        var run = await fixture.PreparedRunAsync("outcome-run", MigrationOperationKind.Validation);
        var attempt = await fixture.Service.StartAttemptAsync(
            fixture.Request, run.RunId, MigrationOperationKind.Validation, "outcome-1", "fp-1");

        var finished = await fixture.FinishAttemptAsync(
            run.RunId, attempt.Value!, MigrationAttemptOutcome.Succeeded);
        Assert.Equal(MigrationAttemptOutcome.Succeeded, finished.Outcome);

        var rewrite = await fixture.Service.RecordAttemptOutcomeAsync(
            fixture.Request,
            run.RunId,
            finished.AttemptId,
            MigrationAttemptOutcome.KnownFailure,
            "rewrite_attempt",
            finished.Version);

        Assert.Equal(MigrationResultKind.Rejected, rewrite.Kind);
        Assert.Equal("migration_attempt_already_finished", rewrite.Code);
    }

    [Fact]
    public async Task Attempt_lineage_is_tenant_scoped()
    {
        await using var fixture = await MigrationFixture.CreateAsync();
        var run = await fixture.PreparedRunAsync("scoped-run", MigrationOperationKind.Validation);
        var attempt = await fixture.Service.StartAttemptAsync(
            fixture.Request, run.RunId, MigrationOperationKind.Validation, "scoped-1", "fp-1");

        var foreignAttempts = await fixture.Persistence.ListAttemptsAsync(fixture.ForeignTenant, run.RunId);
        var foreignAttempt = await fixture.Persistence.FindAttemptAsync(
            fixture.ForeignTenant, run.RunId, attempt.Value!.AttemptId);

        Assert.Empty(foreignAttempts);
        Assert.Null(foreignAttempt);
    }

    [Fact]
    public void Lineage_derivation_fails_closed_on_a_foreign_row()
    {
        var runId = Guid.NewGuid();
        var foreign = new MigrationAttemptRecord(
            Guid.NewGuid(),
            new TenantId(TenantB),
            runId,
            1,
            null,
            MigrationOperationKind.Validation,
            MigrationAttemptOutcome.Succeeded,
            "key",
            "fingerprint",
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            "ok",
            [1]);

        var lineage = MigrationAttemptLineage.FromPersistedAttempts(new TenantId(TenantA), runId, [foreign]);

        Assert.False(lineage.CanStartNext);
        Assert.Equal("migration_attempt_lineage_foreign_row", lineage.BlockingCode);
    }

    // ---------------------------------------------------------------- F3 ----

    [Fact]
    public async Task Concurrent_run_creation_yields_one_success_and_replays_for_every_other_caller()
    {
        await using var fixture = await MigrationFixture.CreateFileBackedAsync();
        const int callers = 8;

        var results = await Task.WhenAll(Enumerable.Range(0, callers).Select(_ => Task.Run(() =>
            fixture.Service.CreateRunAsync(
                fixture.Request,
                Request(null, MigrationOperationKind.Execution, "race-key", "race-fingerprint")))));

        Assert.Equal(1, results.Count(result => result.Kind == MigrationResultKind.Succeeded));
        Assert.Equal(callers - 1, results.Count(result => result.Kind == MigrationResultKind.Replayed));
        Assert.DoesNotContain(results, result => result.Kind == MigrationResultKind.Rejected);
        Assert.DoesNotContain(results, result => result.Kind == MigrationResultKind.UnknownOutcome);
        Assert.Single(results.Select(result => result.Value!.RunId).Distinct());
    }

    [Fact]
    public async Task A_differing_fingerprint_on_the_same_key_is_an_idempotency_conflict()
    {
        await using var fixture = await MigrationFixture.CreateFileBackedAsync();

        var first = await fixture.Service.CreateRunAsync(
            fixture.Request, Request(null, MigrationOperationKind.Execution, "shared", "fingerprint-one"));
        var conflict = await fixture.Service.CreateRunAsync(
            fixture.Request, Request(null, MigrationOperationKind.Execution, "shared", "fingerprint-two"));

        Assert.True(first.Succeeded, first.Code);
        Assert.Equal(MigrationResultKind.Rejected, conflict.Kind);
        Assert.Equal("migration_idempotency_conflict", conflict.Code);
    }

    [Fact]
    public async Task Concurrent_attempt_start_on_one_key_yields_one_success_and_replays()
    {
        await using var fixture = await MigrationFixture.CreateFileBackedAsync();
        var run = await fixture.PreparedRunAsync("attempt-race", MigrationOperationKind.Validation);
        const int callers = 8;

        var results = await Task.WhenAll(Enumerable.Range(0, callers).Select(_ => Task.Run(() =>
            fixture.Service.StartAttemptAsync(
                fixture.Request,
                run.RunId,
                MigrationOperationKind.Validation,
                "attempt-race-key",
                "attempt-race-fingerprint"))));

        Assert.Equal(1, results.Count(result => result.Kind == MigrationResultKind.Succeeded));
        Assert.Equal(callers - 1, results.Count(result => result.Kind == MigrationResultKind.Replayed));
        Assert.DoesNotContain(results, result => result.Kind == MigrationResultKind.UnknownOutcome);
        Assert.Single(results.Select(result => result.Value!.AttemptId).Distinct());
        Assert.All(results, result => Assert.Equal(1, result.Value!.Sequence));
    }

    [Fact]
    public async Task A_missing_run_is_reported_as_not_found_rather_than_a_conflict()
    {
        // Only a uniqueness race may be resolved into a replay. Every other
        // refusal keeps its own classification, so an absent run is never
        // reported as a duplicate of a competing record that does not exist.
        await using var fixture = await MigrationFixture.CreateAsync();
        var missing = await fixture.Persistence.StartAttemptAsync(
            fixture.Tenant,
            new StartMigrationAttemptCommand(
                Guid.NewGuid(),
                MigrationOperationKind.Validation,
                new MigrationIdempotencyKey("absent"),
                new MigrationRequestFingerprint("absent-fingerprint")));

        Assert.Equal(MigrationPersistenceOutcome.NotFound, missing.Outcome);
        Assert.Equal("migration_run_not_found", missing.Code);
    }

    [Fact]
    public void Only_a_uniqueness_violation_is_resolvable_into_a_replay()
    {
        // A non-uniqueness write failure must reach the unproven-outcome path,
        // never the replay path, because no competing record exists to replay.
        // The classifier is the single place that decides, so it is asserted
        // directly rather than by trying to provoke an arbitrary provider fault.
        var classifier = typeof(MigrationPersistence).GetMethod(
            "IsUniqueViolation",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(classifier);

        var unrelated = new DbUpdateException("write failed", new TimeoutException());
        Assert.False((bool)classifier!.Invoke(null, [unrelated])!);
    }

    // ---------------------------------------------------------------- F4 ----

    [Fact]
    public async Task Run_creation_appends_exactly_one_durable_evidence_record()
    {
        await using var fixture = await MigrationFixture.CreateAsync();

        var created = await fixture.Service.CreateRunAsync(
            fixture.Request, Request(null, MigrationOperationKind.Validation, "evidence-key", "evidence-fp"));

        Assert.True(created.Succeeded, created.Code);
        var evidence = Assert.Single(fixture.Audit.Appended);
        Assert.Equal("migration.run.create", evidence.OperationId);
        Assert.Equal("migration", evidence.Source);
        Assert.Equal("migration-run", evidence.TargetType);
        Assert.Equal(TenantA, evidence.TenantId!.Value);
        Assert.Equal(Actor, evidence.ActorId);
        Assert.Equal(FoundationAuditDecision.Allowed, evidence.Decision);
        Assert.Contains($"run={created.Value!.RunId:D}", evidence.TargetReference!, StringComparison.Ordinal);
        Assert.DoesNotContain("attempt=", evidence.TargetReference!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Attempt_creation_and_transitions_each_append_exactly_one_evidence_record()
    {
        await using var fixture = await MigrationFixture.CreateAsync();
        var created = await fixture.CreateRunAsync("evidence-flow", MigrationOperationKind.Validation);
        Assert.Single(fixture.Audit.Appended);

        var prepared = await fixture.TransitionAsync(created, MigrationRunStatus.Prepared);
        Assert.Equal(2, fixture.Audit.Appended.Count);
        var transitionEvidence = fixture.Audit.Appended[1];
        Assert.Equal("migration.run.transition", transitionEvidence.OperationId);
        Assert.Contains("from=Draft", transitionEvidence.ChangeSummary!, StringComparison.Ordinal);
        Assert.Contains("to=Prepared", transitionEvidence.ChangeSummary!, StringComparison.Ordinal);

        var attempt = await fixture.Service.StartAttemptAsync(
            fixture.Request, prepared.RunId, MigrationOperationKind.Validation, "evidence-attempt", "fp");
        Assert.Equal(3, fixture.Audit.Appended.Count);
        var attemptEvidence = fixture.Audit.Appended[2];
        Assert.Equal("migration.attempt.start", attemptEvidence.OperationId);
        Assert.Equal(1, attemptEvidence.Attempt);
        Assert.Contains($"attempt={attempt.Value!.AttemptId:D}", attemptEvidence.TargetReference!, StringComparison.Ordinal);

        await fixture.FinishAttemptAsync(prepared.RunId, attempt.Value, MigrationAttemptOutcome.Succeeded);
        Assert.Equal(4, fixture.Audit.Appended.Count);
        Assert.Equal("migration.attempt.outcome", fixture.Audit.Appended[3].OperationId);
    }

    [Fact]
    public async Task A_replay_is_evidenced_as_a_replay_rather_than_a_new_effect()
    {
        await using var fixture = await MigrationFixture.CreateAsync();
        var request = Request(null, MigrationOperationKind.Validation, "replay-key", "replay-fp");

        await fixture.Service.CreateRunAsync(fixture.Request, request);
        var replay = await fixture.Service.CreateRunAsync(fixture.Request, request);

        Assert.Equal(MigrationResultKind.Replayed, replay.Kind);
        Assert.Equal(2, fixture.Audit.Appended.Count);
        Assert.Contains("replayed=true", fixture.Audit.Appended[1].ChangeSummary!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_rejected_operation_is_still_evidenced()
    {
        await using var fixture = await MigrationFixture.CreateAsync();
        var created = await fixture.CreateRunAsync("rejected-evidence", MigrationOperationKind.Validation);
        fixture.Audit.Appended.Clear();

        var refused = await fixture.Service.TransitionRunAsync(
            fixture.Request, created.RunId, MigrationRunStatus.Completed, created.Version);

        Assert.Equal(MigrationResultKind.Rejected, refused.Kind);
        var evidence = Assert.Single(fixture.Audit.Appended);
        Assert.Equal(FoundationAuditDecision.Denied, evidence.Decision);
        Assert.Equal(FoundationAuditReason.ValidationFailed, evidence.Reason);
    }

    [Fact]
    public async Task An_effect_that_cannot_be_evidenced_is_reported_as_an_unprovable_outcome()
    {
        // The run is written but the mandatory evidence append fails. The
        // effect therefore cannot be proved, which is exactly the BRD section
        // 13.7 condition requiring reconciliation, so it must not be reported
        // as success and must not be automatically replayed.
        await using var fixture = await MigrationFixture.CreateAsync(failAudit: true);

        var created = await fixture.Service.CreateRunAsync(
            fixture.Request, Request(null, MigrationOperationKind.Validation, "no-audit", "no-audit-fp"));

        Assert.Equal(MigrationResultKind.UnknownOutcome, created.Kind);
        Assert.Equal("migration_audit_evidence_unavailable", created.Code);
        Assert.False(created.IsSafeToRetry);
    }

    [Fact]
    public void Evidence_metadata_is_allowlisted_and_bounded()
    {
        var metadata = MigrationAuditMetadata.Create()
            .With("from", MigrationRunStatus.Draft.ToString())
            .With("to", MigrationRunStatus.Prepared.ToString())
            .With("sequence", 3);

        Assert.Equal("from=Draft;sequence=3;to=Prepared", metadata.Render());
        Assert.Throws<ArgumentException>(() => MigrationAuditMetadata.Create().With("rawPayload", "secret"));
        Assert.Throws<ArgumentException>(() => MigrationAuditMetadata.Create().With("from", new string('x', 64)));
        Assert.Throws<ArgumentException>(() => MigrationAuditMetadata.Create().With("from", "a;b"));
        Assert.Throws<ArgumentException>(() => MigrationAuditMetadata.Create().With("from", "a=b"));
    }

    [Fact]
    public void Run_level_evidence_is_expressible_without_an_attempt()
    {
        var tenantContext = Context(TenantA, "migration-audit");
        var requestContext = FoundationRequestContext.ForTenant(
            Actor, Session, tenantContext, "tenant.migration.foundation");
        var run = MigrationRun.Create(tenantContext, Definition(), Profile());

        var evidence = MigrationAuditEvidenceFactory.Create(
            requestContext,
            run,
            "migration.run.create",
            FoundationAuditDecision.Allowed,
            FoundationAuditReason.Allowed,
            "run_created",
            attempt: null,
            idempotencyKey: new MigrationIdempotencyKey("audit-key"),
            safeMetadata: MigrationAuditMetadata.Create().With("to", run.Status.ToString()));

        Assert.Equal(TenantA, evidence.TenantId!.Value);
        Assert.Equal(Actor, evidence.ActorId);
        Assert.Equal("migration", evidence.Source);
        Assert.Equal("migration-run", evidence.TargetType);
        Assert.Equal($"run={run.RunId:D}", evidence.TargetReference);
        Assert.Equal("audit-key", evidence.IdempotencyKey);
        Assert.Contains("outcome=run_created", evidence.ChangeSummary!, StringComparison.Ordinal);

        var contract = MigrationAuditEvidenceFactory.ToContract(evidence, run, attempt: null, "run_created");
        Assert.Null(contract.AttemptId);
        Assert.Equal(run.RunId, contract.RunId);
        Assert.Equal(TenantA, contract.TenantId);
    }

    [Fact]
    public void Evidence_refuses_a_run_and_tenant_boundary_mismatch()
    {
        var tenantContext = Context(TenantA, "migration-audit");
        var foreignContext = FoundationRequestContext.ForTenant(
            Actor, Session, Context(TenantB, "migration-foreign"), "tenant.migration.foundation");
        var run = MigrationRun.Create(tenantContext, Definition(), Profile());

        Assert.Throws<ArgumentException>(() => MigrationAuditEvidenceFactory.Create(
            foreignContext,
            run,
            "migration.run.create",
            FoundationAuditDecision.Allowed,
            FoundationAuditReason.Allowed,
            "run_created"));
    }

    // ------------------------------------------------------- Tenant scope ---

    [Fact]
    public async Task Run_lookup_and_idempotency_are_tenant_scoped()
    {
        await using var fixture = await MigrationFixture.CreateAsync();

        var first = await fixture.Service.CreateRunAsync(
            fixture.Request, Request(null, MigrationOperationKind.DryRun, "shared-key", "same-request"));
        var replay = await fixture.Service.CreateRunAsync(
            fixture.Request, Request(null, MigrationOperationKind.DryRun, "shared-key", "same-request"));
        var tenantBRun = await fixture.Service.CreateRunAsync(
            fixture.ForeignRequest, Request(null, MigrationOperationKind.DryRun, "shared-key", "same-request"));
        var separateOperation = await fixture.Service.CreateRunAsync(
            fixture.Request, Request(null, MigrationOperationKind.Validation, "shared-key", "same-request"));

        Assert.True(first.Succeeded, first.Code);
        Assert.Equal(MigrationResultKind.Replayed, replay.Kind);
        Assert.Equal(first.Value!.RunId, replay.Value!.RunId);
        Assert.True(tenantBRun.Succeeded, tenantBRun.Code);
        Assert.NotEqual(first.Value.RunId, tenantBRun.Value!.RunId);
        Assert.True(separateOperation.Succeeded, separateOperation.Code);
        Assert.NotEqual(first.Value.RunId, separateOperation.Value!.RunId);

        var foreignRead = await fixture.Service.FindRunAsync(fixture.ForeignTenant, first.Value.RunId);
        Assert.False(foreignRead.Succeeded);
        Assert.Equal("migration_run_not_found", foreignRead.Code);
    }

    [Fact]
    public async Task A_client_supplied_tenant_cannot_override_the_server_tenant()
    {
        await using var fixture = await MigrationFixture.CreateAsync();

        var rejected = await fixture.Service.CreateRunAsync(
            fixture.Request, Request(TenantB, MigrationOperationKind.Validation, "override-key", "fp"));

        Assert.Equal(MigrationResultKind.Rejected, rejected.Kind);
        Assert.Equal("migration_tenant_context_mismatch", rejected.Code);
    }

    [Fact]
    public void Migration_module_does_not_take_cross_module_persistence_dependencies()
    {
        var root = FindRepositoryRoot();
        var source = Path.Combine(root, "backend", "src");
        // Compared with Path.Combine rather than a hard-coded separator so the
        // boundary is actually enforced on every platform the suite runs on.
        var migrationFolders = new[]
        {
            Path.Combine("MiniErp.App", "Modules", "Migration"),
            Path.Combine("MiniErp.Contracts", "Modules", "Migration"),
            Path.Combine("MiniErp.Infrastructure", "Persistence", "Modules", "Migration")
        };
        var files = Directory.GetFiles(source, "*.cs", SearchOption.AllDirectories)
            .Where(path => migrationFolders.Any(folder => path.Contains(folder, StringComparison.Ordinal)))
            .ToArray();

        // Derived from the module folders that actually exist, so a module
        // added later is forbidden by default instead of silently unchecked.
        // Audit is the one permitted dependency: it owns the foundation
        // evidence seam every module appends to.
        var permitted = new[] { "Migration", "Audit" };
        var forbidden = Directory.GetDirectories(Path.Combine(source, "MiniErp.App", "Modules"))
            .Select(Path.GetFileName)
            .Where(module => !permitted.Contains(module, StringComparer.Ordinal))
            .ToArray();

        Assert.NotEmpty(files);
        Assert.Contains("Reporting", forbidden);
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            foreach (var module in forbidden)
            {
                Assert.False(
                    text.Contains($"Modules.{module}", StringComparison.Ordinal),
                    $"{Path.GetFileName(file)} depends on the {module} module.");
            }
        }
    }

    // ------------------------------------------------------------ helpers ---

    private static MigrationRun Draft() => MigrationRun.Create(Context(TenantA, "migration-state"), Definition(), Profile());

    private static MigrationRun RunAt(MigrationRunStatus status) => MigrationRun.Rehydrate(new MigrationRunRecord(
        Guid.NewGuid(),
        new TenantId(TenantA),
        Actor,
        new CorrelationId("migration-state"),
        Definition(),
        Profile(),
        status,
        DateTimeOffset.UnixEpoch,
        DateTimeOffset.UnixEpoch,
        [1]));

    private static MigrationRunCreationRequest Request(
        Guid? requestedTenantId,
        MigrationOperationKind operation,
        string key,
        string fingerprint) => new(
        requestedTenantId,
        Definition(),
        Profile(),
        operation,
        key,
        fingerprint);

    private static MigrationDefinitionReference Definition() => new("tenant-onboarding.foundation", "1");

    private static MigrationSourceProfileReference Profile() => new("neutral-source-profile", "1");

    private static TenantContext Context(Guid tenantId, string correlation) =>
        TenantContext.ForOrdinaryMembership(
            new TenantId(tenantId),
            new MembershipReference(Guid.NewGuid()),
            correlationId: new CorrelationId(correlation),
            actorId: Actor);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    /// <summary>Records every appended evidence record for assertion.</summary>
    private sealed class RecordingAuditSink(bool fail) : IFoundationAuditEvidenceSink
    {
        public List<FoundationAuditEvidence> Appended { get; } = [];

        public ValueTask AppendAsync(
            FoundationAuditEvidence evidence,
            CancellationToken cancellationToken = default)
        {
            if (fail)
            {
                throw new FoundationAuditAppendException("evidence_store_unavailable");
            }

            lock (Appended)
            {
                Appended.Add(evidence);
            }

            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Runs one armed operation to completion immediately before the next
    /// read of the attempts table, forcing a commit into the gap between a
    /// caller's idempotency read and its lineage read.
    /// </summary>
    private sealed class CommitBeforeAttemptsReadInterceptor : DbCommandInterceptor
    {
        private Func<Task<MigrationOperationResult<MigrationAttemptRecord>>>? pending;

        internal MigrationOperationResult<MigrationAttemptRecord>? WinnerResult { get; private set; }

        internal void Arm(Func<Task<MigrationOperationResult<MigrationAttemptRecord>>> commit) =>
            pending = commit;

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FROM \"MigrationAttempts\"", StringComparison.Ordinal)
                && Interlocked.Exchange(ref pending, null) is { } commit)
            {
                WinnerResult = await commit();
            }

            return result;
        }
    }

    /// <summary>
    /// Disposable Migration foundation fixture over a disposable SQLite
    /// database. The file-backed variant exists because a shared in-memory
    /// connection serializes callers and cannot exercise a real write race.
    /// </summary>
    private sealed class MigrationFixture : IAsyncDisposable
    {
        private readonly SqliteConnection? connection;
        private readonly string? databasePath;

        private MigrationFixture(
            DbContextOptions options,
            SqliteConnection? connection,
            string? databasePath,
            RecordingAuditSink audit)
        {
            this.connection = connection;
            this.databasePath = databasePath;
            Audit = audit;
            Persistence = new MigrationPersistence(options);
            Service = new MigrationFoundationService(Persistence, audit);
            Tenant = Context(TenantA, "migration-fixture");
            ForeignTenant = Context(TenantB, "migration-fixture-foreign");
            Request = FoundationRequestContext.ForTenant(Actor, Session, Tenant, "tenant.migration.foundation");
            ForeignRequest = FoundationRequestContext.ForTenant(
                Actor, Session, ForeignTenant, "tenant.migration.foundation");
        }

        internal RecordingAuditSink Audit { get; }

        internal MigrationPersistence Persistence { get; }

        internal MigrationFoundationService Service { get; }

        internal TenantContext Tenant { get; }

        internal TenantContext ForeignTenant { get; }

        internal FoundationRequestContext Request { get; }

        internal FoundationRequestContext ForeignRequest { get; }

        internal static async Task<MigrationFixture> CreateAsync(bool failAudit = false)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
            var fixture = new MigrationFixture(options, connection, null, new RecordingAuditSink(failAudit));
            await fixture.EnsureCreatedAsync(options);
            return fixture;
        }

        internal static async Task<MigrationFixture> CreateFileBackedAsync(IInterceptor? interceptor = null)
        {
            var databasePath = Path.Combine(
                Path.GetTempPath(),
                $"MiniErpMigration_{Guid.NewGuid():N}.db");
            var builder = new DbContextOptionsBuilder().UseSqlite($"Data Source={databasePath}");
            if (interceptor is not null)
            {
                builder.AddInterceptors(interceptor);
            }

            var options = builder.Options;
            var fixture = new MigrationFixture(options, null, databasePath, new RecordingAuditSink(false));
            await fixture.EnsureCreatedAsync(options);
            return fixture;
        }

        internal async Task<MigrationRunRecord> CreateRunAsync(string key, MigrationOperationKind operation)
        {
            var created = await Service.CreateRunAsync(Request, CreationRequest(key, operation));
            Assert.True(created.Succeeded, created.Code);
            return created.Value!;
        }

        /// <summary>Creates a run and moves it to Prepared, where validation may start.</summary>
        internal async Task<MigrationRunRecord> PreparedRunAsync(string key, MigrationOperationKind operation)
        {
            var created = await CreateRunAsync(key, operation);
            return await TransitionAsync(created, MigrationRunStatus.Prepared);
        }

        internal async Task<MigrationRunRecord> TransitionAsync(
            MigrationRunRecord run,
            MigrationRunStatus target)
        {
            var applied = await Service.TransitionRunAsync(Request, run.RunId, target, run.Version);
            Assert.True(applied.Succeeded, applied.Code);
            return applied.Value!;
        }

        internal async Task<MigrationAttemptRecord> FinishAttemptAsync(
            Guid runId,
            MigrationAttemptRecord attempt,
            MigrationAttemptOutcome outcome)
        {
            var recorded = await Service.RecordAttemptOutcomeAsync(
                Request, runId, attempt.AttemptId, outcome, outcome.ToString(), attempt.Version);
            Assert.NotNull(recorded.Value);
            return recorded.Value!;
        }

        public async ValueTask DisposeAsync()
        {
            if (connection is not null)
            {
                await connection.DisposeAsync();
            }

            if (databasePath is not null)
            {
                SqliteConnection.ClearAllPools();
                try
                {
                    File.Delete(databasePath);
                }
                catch (IOException)
                {
                    // A disposable test database left behind is harmless.
                }
            }
        }

        private async Task EnsureCreatedAsync(DbContextOptions options)
        {
            await using var db = new MigrationDbContext(options, Tenant);
            await db.Database.EnsureCreatedAsync();
        }

        private static MigrationRunCreationRequest CreationRequest(string key, MigrationOperationKind operation) =>
            MigrationFoundationTests.Request(null, operation, key, $"{key}-fingerprint");
    }
}
