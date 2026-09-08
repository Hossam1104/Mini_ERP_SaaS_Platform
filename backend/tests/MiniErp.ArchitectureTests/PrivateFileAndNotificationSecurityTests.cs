using System.Text;
using Microsoft.Extensions.DependencyInjection;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.BuildingBlocks.Work;
using MiniErp.App.Modules.Identity;
using Xunit;

namespace MiniErp.ArchitectureTests;

/// <summary>
/// MESP-93 focused correction: closes M-1 (foreign vs missing file existence
/// oracle), M-4 (invalid-state object overwrite), M-5 (unsafe Unicode
/// filename controls), M-7 (unbounded notification retry), M-8 (unverified
/// notification recipient), M-9 (untested returned-content immutability) and
/// L-4 (dead AnonymousDenied outcome / misleading disposition cleanup).
/// </summary>
public sealed class PrivateFileAndNotificationSecurityTests
{
    // -------------------------------------------------------------------
    // M-1: foreign vs missing file existence oracle
    // -------------------------------------------------------------------

    [Fact]
    public async Task Foreign_tenant_object_and_missing_object_are_indistinguishable_on_read()
    {
        var owner = CreateContext();
        var storage = new InMemoryPrivateObjectStorage();
        var metadata = await storage.StoreAsync(owner, DurableWorkTestSupport.TenantWideScope(owner), "secret.txt", "text/plain", Content("secret"));

        var foreignResult = await storage.ReadAsync(CreateContext(), metadata.ObjectId);
        var missingResult = await storage.ReadAsync(CreateContext(), Guid.NewGuid());

        Assert.Equal(PrivateFileAccessOutcome.NotFound, foreignResult.Outcome);
        Assert.Equal(missingResult.Outcome, foreignResult.Outcome);
        Assert.Null(foreignResult.Metadata);
        Assert.Null(foreignResult.Content);
        Assert.False(foreignResult.Allowed);
    }

    [Fact]
    public async Task Foreign_tenant_object_and_missing_object_are_indistinguishable_on_overwrite()
    {
        var owner = CreateContext();
        var storage = new InMemoryPrivateObjectStorage();
        var metadata = await storage.StoreAsync(owner, DurableWorkTestSupport.TenantWideScope(owner), "secret.txt", "text/plain", Content("secret"));

        var foreignResult = await storage.OverwriteAsync(CreateContext(), metadata.ObjectId, metadata.ConcurrencyVersion, Content("forged"));
        var missingResult = await storage.OverwriteAsync(CreateContext(), Guid.NewGuid(), 1, Content("forged"));

        Assert.Equal(PrivateFileAccessOutcome.NotFound, foreignResult.Outcome);
        Assert.Equal(missingResult.Outcome, foreignResult.Outcome);
        Assert.False(foreignResult.Mutated);
        Assert.False(missingResult.Mutated);
    }

    // -------------------------------------------------------------------
    // M-4: expired/invalid objects fail closed on overwrite
    // -------------------------------------------------------------------

    [Fact]
    public async Task Expired_object_cannot_be_overwritten()
    {
        var context = CreateContext();
        var storage = new InMemoryPrivateObjectStorage();
        var metadata = await storage.StoreAsync(
            context, DurableWorkTestSupport.TenantWideScope(context), "old.txt", "text/plain", Content("old"), DateTimeOffset.UtcNow.AddMinutes(-1));

        var result = await storage.OverwriteAsync(context, metadata.ObjectId, metadata.ConcurrencyVersion, Content("new"));

        Assert.Equal(PrivateFileAccessOutcome.Expired, result.Outcome);
        Assert.False(result.Mutated);
        Assert.True(storage.ExistsForValidation(metadata.ObjectId));
    }

    [Fact]
    public async Task Checksum_failed_object_cannot_be_overwritten()
    {
        var context = CreateContext();
        var storage = new InMemoryPrivateObjectStorage();
        var metadata = await storage.StoreAsync(context, DurableWorkTestSupport.TenantWideScope(context), "tampered.txt", "text/plain", Content("safe"));
        storage.TamperForValidation(metadata.ObjectId, "tampered"u8.ToArray());

        var result = await storage.OverwriteAsync(context, metadata.ObjectId, metadata.ConcurrencyVersion, Content("new"));

        Assert.Equal(PrivateFileAccessOutcome.ChecksumFailed, result.Outcome);
        Assert.False(result.Mutated);
    }

    [Fact]
    public async Task Available_unexpired_object_replacement_requires_new_scan_evidence()
    {
        var context = CreateContext();
        var storage = new InMemoryPrivateObjectStorage();
        var metadata = await storage.StoreAsync(context, DurableWorkTestSupport.TenantWideScope(context), "ok.txt", "text/plain", Content("ok"));

        metadata.ScanState = PrivateFileScanState.Clean;
        var versionBefore = metadata.ConcurrencyVersion;
        var result = await storage.OverwriteAsync(context, metadata.ObjectId, metadata.ConcurrencyVersion, Content("updated"));

        // The write is genuinely applied and quarantined pending new scan
        // evidence -- SafetyBlocked must never be misread by a caller as "the
        // mutation failed" (HOLD-140-C): Mutated is true, content stays
        // withheld, and the concurrency version advances to reflect the
        // real state change.
        Assert.Equal(PrivateFileAccessOutcome.SafetyBlocked, result.Outcome);
        Assert.True(result.Mutated);
        Assert.Null(result.Content);
        Assert.NotNull(result.Metadata);
        Assert.Equal(PrivateFileScanState.Unavailable, metadata.ScanState);
        Assert.Equal(versionBefore + 1, metadata.ConcurrencyVersion);
    }

    [Theory]
    [InlineData(PrivateFileScanState.NotScanned)]
    [InlineData(PrivateFileScanState.Pending)]
    [InlineData(PrivateFileScanState.Unavailable)]
    [InlineData(PrivateFileScanState.Rejected)]
    public async Task Scan_required_content_is_unavailable_until_clean_evidence(
        PrivateFileScanState state)
    {
        var context = CreateContext();
        var storage = new InMemoryPrivateObjectStorage();
        var metadata = await storage.StoreAsync(context, DurableWorkTestSupport.TenantWideScope(context), "scan-required.txt", "text/plain", Content("private"));
        metadata.ScanState = state;

        var blocked = await storage.ReadAsync(context, metadata.ObjectId);

        Assert.Equal(PrivateFileAccessOutcome.SafetyBlocked, blocked.Outcome);
        Assert.Null(blocked.Content);
    }

    [Fact]
    public async Task Clean_evidence_allows_same_tenant_read_without_minting_clean_state()
    {
        var context = CreateContext();
        var storage = new InMemoryPrivateObjectStorage();
        var metadata = await storage.StoreAsync(context, DurableWorkTestSupport.TenantWideScope(context), "approved.txt", "text/plain", Content("private"));
        metadata.ScanState = PrivateFileScanState.Clean;

        var read = await storage.ReadAsync(context, metadata.ObjectId);

        Assert.True(read.Allowed);
        Assert.Equal("private", Encoding.UTF8.GetString(read.Content!));
    }

    [Fact]
    public async Task Trusted_generated_artifact_is_explicitly_not_applicable_to_scanning()
    {
        var context = CreateContext();
        var storage = new InMemoryPrivateObjectStorage();
        var metadata = await storage.StoreAsync(
            context,
            DurableWorkTestSupport.TenantWideScope(context),
            "report.csv",
            "text/csv",
            Content("generated"),
            safetyRequirement: PrivateFileSafetyRequirement.TrustedGenerated);

        var read = await storage.ReadAsync(context, metadata.ObjectId);

        Assert.Equal(PrivateFileSafetyRequirement.TrustedGenerated, metadata.SafetyRequirement);
        Assert.Equal(PrivateFileScanState.NotApplicable, metadata.ScanState);
        Assert.True(read.Allowed);
        Assert.Equal("generated", Encoding.UTF8.GetString(read.Content!));
    }

    [Fact]
    public async Task Overwrite_resets_scan_required_content_and_does_not_return_new_bytes()
    {
        var context = CreateContext();
        var storage = new InMemoryPrivateObjectStorage();
        var metadata = await storage.StoreAsync(context, DurableWorkTestSupport.TenantWideScope(context), "replacement.txt", "text/plain", Content("v1"));
        metadata.ScanState = PrivateFileScanState.Clean;

        var overwritten = await storage.OverwriteAsync(context, metadata.ObjectId, metadata.ConcurrencyVersion, Content("v2"));
        var blocked = await storage.ReadAsync(context, metadata.ObjectId);

        Assert.Equal(PrivateFileAccessOutcome.SafetyBlocked, overwritten.Outcome);
        Assert.True(overwritten.Mutated);
        Assert.Null(overwritten.Content);
        Assert.Equal(PrivateFileScanState.Unavailable, metadata.ScanState);
        Assert.Equal(PrivateFileAccessOutcome.SafetyBlocked, blocked.Outcome);
    }

    [Fact]
    public async Task Already_expired_disposition_is_reported_as_expired_on_read_and_overwrite()
    {
        var context = CreateContext();
        var storage = new InMemoryPrivateObjectStorage();
        var metadata = await storage.StoreAsync(context, DurableWorkTestSupport.TenantWideScope(context), "already-expired.txt", "text/plain", Content("x"));
        metadata.Disposition = PrivateFileDisposition.Expired;

        var read = await storage.ReadAsync(context, metadata.ObjectId);
        var overwrite = await storage.OverwriteAsync(context, metadata.ObjectId, metadata.ConcurrencyVersion, Content("y"));

        Assert.Equal(PrivateFileAccessOutcome.Expired, read.Outcome);
        Assert.Equal(PrivateFileAccessOutcome.Expired, overwrite.Outcome);
        Assert.False(overwrite.Mutated);
    }

    [Fact]
    public async Task Already_checksum_failed_disposition_is_reported_as_checksum_failed_not_expired()
    {
        // A previously recorded ChecksumFailed disposition must not be
        // misleadingly folded into Expired just because it is not Available
        // (M93-02).
        var context = CreateContext();
        var storage = new InMemoryPrivateObjectStorage();
        var metadata = await storage.StoreAsync(context, DurableWorkTestSupport.TenantWideScope(context), "already-checksum-failed.txt", "text/plain", Content("x"));
        metadata.Disposition = PrivateFileDisposition.ChecksumFailed;

        var read = await storage.ReadAsync(context, metadata.ObjectId);
        var overwrite = await storage.OverwriteAsync(context, metadata.ObjectId, metadata.ConcurrencyVersion, Content("y"));

        Assert.Equal(PrivateFileAccessOutcome.ChecksumFailed, read.Outcome);
        Assert.Equal(PrivateFileAccessOutcome.ChecksumFailed, overwrite.Outcome);
        Assert.False(overwrite.Mutated);
    }

    [Fact]
    public async Task Disposed_disposition_is_reported_as_disposed_not_expired()
    {
        // A Disposed object must not be misleadingly folded into Expired
        // either (M93-02); it gets its own accurate, safe outcome.
        var context = CreateContext();
        var storage = new InMemoryPrivateObjectStorage();
        var metadata = await storage.StoreAsync(context, DurableWorkTestSupport.TenantWideScope(context), "disposed.txt", "text/plain", Content("x"));
        metadata.Disposition = PrivateFileDisposition.Disposed;

        var read = await storage.ReadAsync(context, metadata.ObjectId);
        var overwrite = await storage.OverwriteAsync(context, metadata.ObjectId, metadata.ConcurrencyVersion, Content("y"));

        Assert.Equal(PrivateFileAccessOutcome.Disposed, read.Outcome);
        Assert.Equal(PrivateFileAccessOutcome.Disposed, overwrite.Outcome);
        Assert.False(read.Allowed);
        Assert.False(overwrite.Allowed);
        Assert.False(overwrite.Mutated);
    }

    [Fact]
    public async Task No_rejected_overwrite_changes_content_or_concurrency_version()
    {
        var context = CreateContext();
        var storage = new InMemoryPrivateObjectStorage();
        var metadata = await storage.StoreAsync(
            context, DurableWorkTestSupport.TenantWideScope(context), "guarded.txt", "text/plain", Content("original"), DateTimeOffset.UtcNow.AddMinutes(-1));
        var versionBefore = metadata.ConcurrencyVersion;

        var overwrite = await storage.OverwriteAsync(context, metadata.ObjectId, metadata.ConcurrencyVersion, Content("forged"));
        var read = await storage.ReadAsync(context, metadata.ObjectId);

        Assert.False(overwrite.Allowed);
        Assert.False(overwrite.Mutated);
        Assert.Equal(versionBefore, metadata.ConcurrencyVersion);
        Assert.Equal(PrivateFileAccessOutcome.Expired, read.Outcome);
    }

    // -------------------------------------------------------------------
    // M-5: Unicode filename security
    // -------------------------------------------------------------------

    [Fact]
    public async Task Valid_english_filename_is_accepted()
    {
        var context = CreateContext();
        var storage = new InMemoryPrivateObjectStorage();
        const string fileName = "invoice.pdf";

        var metadata = await storage.StoreAsync(context, DurableWorkTestSupport.TenantWideScope(context), fileName, "text/plain", Content("ok"));

        Assert.Equal(fileName, metadata.OriginalFileName);
    }

    [Fact]
    public async Task Valid_arabic_filename_is_accepted()
    {
        var context = CreateContext();
        var storage = new InMemoryPrivateObjectStorage();
        // "فاتورة.pdf" spells the Arabic word for
        // "invoice" (fatura) followed by the .pdf extension.
        const string fileName = "فاتورة.pdf";

        var metadata = await storage.StoreAsync(context, DurableWorkTestSupport.TenantWideScope(context), fileName, "text/plain", Content("ok"));

        Assert.Equal(fileName, metadata.OriginalFileName);
    }

    [Fact]
    public async Task Mixed_arabic_and_english_filename_is_accepted()
    {
        var context = CreateContext();
        var storage = new InMemoryPrivateObjectStorage();
        const string fileName = "invoice-فاتورة-2026.pdf";

        var metadata = await storage.StoreAsync(context, DurableWorkTestSupport.TenantWideScope(context), fileName, "text/plain", Content("ok"));

        Assert.Equal(fileName, metadata.OriginalFileName);
    }

    [Fact]
    public async Task Composed_and_decomposed_unicode_filenames_normalize_to_the_same_stored_name()
    {
        var context = CreateContext();
        var storage = new InMemoryPrivateObjectStorage();
        const string composed = "café.txt"; // single composed code point U+00E9 (e with acute accent)
        const string decomposed = "café.txt"; // base 'e' (U+0065) + combining acute accent U+0301

        var storedComposed = await storage.StoreAsync(context, DurableWorkTestSupport.TenantWideScope(context), composed, "text/plain", Content("a"));
        var storedDecomposed = await storage.StoreAsync(context, DurableWorkTestSupport.TenantWideScope(context), decomposed, "text/plain", Content("b"));

        Assert.Equal(storedComposed.OriginalFileName, storedDecomposed.OriginalFileName);
    }

    [Theory]
    [InlineData("invoice‮pdf.exe")] // right-to-left override
    [InlineData("invoice‪pdf.exe")] // left-to-right embedding
    [InlineData("invoice‫pdf.exe")] // right-to-left embedding
    [InlineData("invoice‬pdf.exe")] // pop directional formatting
    [InlineData("invoice‭pdf.exe")] // left-to-right override (LRO)
    [InlineData("invoice⁦pdf.exe")] // left-to-right isolate
    [InlineData("invoice⁧pdf.exe")] // right-to-left isolate
    [InlineData("invoice⁨pdf.exe")] // first strong isolate
    [InlineData("invoice⁩pdf.exe")] // pop directional isolate
    [InlineData("invoice‎pdf.exe")] // left-to-right mark
    [InlineData("invoice‏pdf.exe")] // right-to-left mark
    [InlineData("invoice​pdf.exe")] // zero width space
    [InlineData("invoice⁠pdf.exe")] // word joiner
    [InlineData("invoice﻿pdf.exe")] // zero width no-break space / BOM
    public async Task Bidi_and_deceptive_format_characters_are_rejected(string fileName)
    {
        var context = CreateContext();
        var storage = new InMemoryPrivateObjectStorage();

        await Assert.ThrowsAsync<ArgumentException>(() => storage.StoreAsync(
            context, DurableWorkTestSupport.TenantWideScope(context), fileName, "text/plain", Content("x")).AsTask());
    }

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("..\\..\\windows\\system32\\config")]
    [InlineData("..")]
    [InlineData(".")]
    public async Task Traversal_sequences_and_reserved_names_are_rejected(string fileName)
    {
        var context = CreateContext();
        var storage = new InMemoryPrivateObjectStorage();

        await Assert.ThrowsAsync<ArgumentException>(() => storage.StoreAsync(
            context, DurableWorkTestSupport.TenantWideScope(context), fileName, "text/plain", Content("x")).AsTask());
    }

    [Fact]
    public async Task Control_characters_are_rejected()
    {
        var context = CreateContext();
        var storage = new InMemoryPrivateObjectStorage();
        const string fileName = "invoice.pdf";

        await Assert.ThrowsAsync<ArgumentException>(() => storage.StoreAsync(
            context, DurableWorkTestSupport.TenantWideScope(context), fileName, "text/plain", Content("x")).AsTask());
    }

    [Theory]
    [InlineData("report..final.txt")]
    [InlineData("version..2.txt")]
    [InlineData("invoice...pdf")]
    public async Task Filenames_with_embedded_double_dots_are_accepted(string fileName)
    {
        // Separators are already rejected outright, so an embedded ".."
        // substring is no longer a traversal vector; only the exact reserved
        // names "." and ".." remain rejected (L93-01).
        var context = CreateContext();
        var storage = new InMemoryPrivateObjectStorage();

        var metadata = await storage.StoreAsync(context, DurableWorkTestSupport.TenantWideScope(context), fileName, "text/plain", Content("x"));

        Assert.Equal(fileName, metadata.OriginalFileName);
    }

    [Fact]
    public async Task Filenames_with_zero_width_joiner_or_non_joiner_are_accepted()
    {
        // U+200C (ZWNJ) and U+200D (ZWJ) are deliberately outside the
        // rejection policy: they have legitimate Arabic-script shaping uses
        // and are not part of the approved bidi/format rejection list
        // (L93-01) -- unlike the other invisible/format characters covered
        // by Bidi_and_deceptive_format_characters_are_rejected above.
        var context = CreateContext();
        var storage = new InMemoryPrivateObjectStorage();
        var fileName = "invoice" + (char)0x200C + (char)0x200D + ".pdf";

        var metadata = await storage.StoreAsync(context, DurableWorkTestSupport.TenantWideScope(context), fileName, "text/plain", Content("x"));

        Assert.Equal(fileName, metadata.OriginalFileName);
    }

    [Fact]
    public async Task Filename_at_maximum_length_is_accepted_and_beyond_is_rejected()
    {
        var context = CreateContext();
        var storage = new InMemoryPrivateObjectStorage();
        var atLimit = new string('a', 251) + ".txt"; // exactly 255 characters
        var overLimit = new string('a', 252) + ".txt"; // 256 characters

        var metadata = await storage.StoreAsync(context, DurableWorkTestSupport.TenantWideScope(context), atLimit, "text/plain", Content("x"));

        Assert.Equal(255, metadata.OriginalFileName.Length);
        await Assert.ThrowsAsync<ArgumentException>(() => storage.StoreAsync(
            context, DurableWorkTestSupport.TenantWideScope(context), overLimit, "text/plain", Content("x")).AsTask());
    }

    // -------------------------------------------------------------------
    // M-9: returned content is a defensive copy
    // -------------------------------------------------------------------

    [Fact]
    public async Task Mutating_returned_read_content_does_not_affect_a_second_read()
    {
        var context = CreateContext();
        var storage = new InMemoryPrivateObjectStorage();
        var metadata = await storage.StoreAsync(context, DurableWorkTestSupport.TenantWideScope(context), "immutable.txt", "text/plain", Content("original"));
        metadata.ScanState = PrivateFileScanState.Clean;

        var first = await storage.ReadAsync(context, metadata.ObjectId);
        Array.Clear(first.Content!, 0, first.Content!.Length);
        var second = await storage.ReadAsync(context, metadata.ObjectId);

        Assert.Equal("original", Encoding.UTF8.GetString(second.Content!));
    }

    [Fact]
    public async Task Mutating_caller_buffer_after_store_does_not_affect_stored_content()
    {
        var context = CreateContext();
        var storage = new InMemoryPrivateObjectStorage();
        var bytes = Encoding.UTF8.GetBytes("original");
        using var stream = new MemoryStream(bytes);

        var metadata = await storage.StoreAsync(context, DurableWorkTestSupport.TenantWideScope(context), "input.txt", "text/plain", stream);
        metadata.ScanState = PrivateFileScanState.Clean;
        Array.Clear(bytes, 0, bytes.Length);
        var read = await storage.ReadAsync(context, metadata.ObjectId);

        Assert.Equal("original", Encoding.UTF8.GetString(read.Content!));
    }

    [Fact]
    public async Task Approved_replacement_content_is_defensive_after_new_scan_evidence()
    {
        var context = CreateContext();
        var storage = new InMemoryPrivateObjectStorage();
        var metadata = await storage.StoreAsync(context, DurableWorkTestSupport.TenantWideScope(context), "double.txt", "text/plain", Content("v1"));
        metadata.ScanState = PrivateFileScanState.Clean;

        var overwritten = await storage.OverwriteAsync(context, metadata.ObjectId, metadata.ConcurrencyVersion, Content("v2"));
        Assert.Equal(PrivateFileAccessOutcome.SafetyBlocked, overwritten.Outcome);
        // Bytes are genuinely persisted despite the quarantine outcome; the
        // caller must be told the mutation applied (Mutated true) rather than
        // being falsely told it failed, which is exactly what this test's
        // downstream Clean-evidence read proves happened to the content.
        Assert.True(overwritten.Mutated);
        metadata.ScanState = PrivateFileScanState.Clean;
        var read = await storage.ReadAsync(context, metadata.ObjectId);

        Assert.Equal("v2", Encoding.UTF8.GetString(read.Content!));
    }

    // -------------------------------------------------------------------
    // L-4: dead enum member removed
    // -------------------------------------------------------------------

    [Fact]
    public void AnonymousDenied_outcome_no_longer_exists()
    {
        Assert.DoesNotContain("AnonymousDenied", Enum.GetNames<PrivateFileAccessOutcome>());
    }

    // -------------------------------------------------------------------
    // M-7: bounded notification retry with terminal dead-letter
    // -------------------------------------------------------------------

    [Fact]
    public async Task Notification_first_attempt_and_retry_transitions_are_explicit()
    {
        var context = CreateContext();
        var failFirst = true;
        var adapter = new InMemoryNotificationAdapter(_ =>
        {
            if (!failFirst)
            {
                return null;
            }

            failFirst = false;
            return DurableWorkFailureCategory.ProviderUnavailable;
        });
        var intent = TenantNotificationIntent.Create(context, DurableWorkTestSupport.TenantWideScope(context), VerifiedRecipient(context.TenantId), "welcome", "en", "notify-first-retry");

        var firstAttempt = await adapter.DeliverAsync(context, intent);
        Assert.Equal(NotificationDeliveryState.RetryScheduled, firstAttempt.State);
        Assert.Equal(1, intent.AttemptCount);

        var retry = await adapter.DeliverAsync(context, intent);
        Assert.Equal(NotificationDeliveryState.Delivered, retry.State);
        Assert.Equal(2, intent.AttemptCount);
    }

    [Fact]
    public async Task Notification_dead_letters_at_max_attempts_and_never_retries_again()
    {
        var context = CreateContext();
        var invocations = 0;
        var adapter = new InMemoryNotificationAdapter(_ =>
        {
            invocations++;
            return DurableWorkFailureCategory.ProviderUnavailable;
        });
        var intent = TenantNotificationIntent.Create(context, DurableWorkTestSupport.TenantWideScope(context), VerifiedRecipient(context.TenantId), "welcome", "en", "notify-bounded");

        NotificationDeliveryResult? last = null;
        for (var attempt = 0; attempt < TenantNotificationIntent.MaxDeliveryAttempts; attempt++)
        {
            last = await adapter.DeliverAsync(context, intent);
        }

        Assert.Equal(NotificationDeliveryState.DeadLetter, last!.State);
        Assert.Equal(TenantNotificationIntent.MaxDeliveryAttempts, intent.AttemptCount);
        Assert.Equal(TenantNotificationIntent.MaxDeliveryAttempts, invocations);

        var afterDeadLetter = await adapter.DeliverAsync(context, intent);

        Assert.Equal(NotificationDeliveryState.DeadLetter, afterDeadLetter.State);
        Assert.Equal(TenantNotificationIntent.MaxDeliveryAttempts, intent.AttemptCount);
        Assert.Equal(TenantNotificationIntent.MaxDeliveryAttempts, invocations);
    }

    [Fact]
    public async Task Notification_success_before_max_attempts_stops_further_delivery()
    {
        var context = CreateContext();
        var callCount = 0;
        var adapter = new InMemoryNotificationAdapter(_ =>
        {
            callCount++;
            return callCount < 2 ? DurableWorkFailureCategory.ProviderUnavailable : null;
        });
        var intent = TenantNotificationIntent.Create(context, DurableWorkTestSupport.TenantWideScope(context), VerifiedRecipient(context.TenantId), "welcome", "en", "notify-success-before-max");

        var first = await adapter.DeliverAsync(context, intent);
        var second = await adapter.DeliverAsync(context, intent);
        var third = await adapter.DeliverAsync(context, intent);

        Assert.Equal(NotificationDeliveryState.RetryScheduled, first.State);
        Assert.Equal(NotificationDeliveryState.Delivered, second.State);
        Assert.True(third.Duplicate);
        Assert.Equal(2, intent.AttemptCount);
    }

    [Fact]
    public async Task Notification_cancellation_is_explicit_and_does_not_mutate_state()
    {
        var context = CreateContext();
        var adapter = new InMemoryNotificationAdapter();
        var intent = TenantNotificationIntent.Create(context, DurableWorkTestSupport.TenantWideScope(context), VerifiedRecipient(context.TenantId), "welcome", "en", "notify-cancel");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => adapter.DeliverAsync(context, intent, cts.Token).AsTask());

        Assert.Equal(NotificationDeliveryState.Pending, intent.DeliveryState);
        Assert.Equal(0, intent.AttemptCount);
    }

    [Fact]
    public async Task Concurrent_duplicate_delivery_claims_are_a_single_effect()
    {
        var context = CreateContext();
        var adapter = new InMemoryNotificationAdapter();
        var intent = TenantNotificationIntent.Create(context, DurableWorkTestSupport.TenantWideScope(context), VerifiedRecipient(context.TenantId), "welcome", "en", "notify-concurrent");

        var results = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => adapter.DeliverAsync(context, intent).AsTask()));

        Assert.Single(results, result => result.Delivered && !result.Duplicate);
        Assert.Equal(7, results.Count(result => result.Duplicate));
        Assert.Equal(1, intent.AttemptCount);
    }

    // -------------------------------------------------------------------
    // H93-01: an unauthorized (wrong-Tenant) delivery attempt must never
    // mutate the owner Tenant's intent
    // -------------------------------------------------------------------

    [Fact]
    public async Task Wrong_tenant_delivery_returns_a_safe_denial()
    {
        var owner = CreateContext();
        var adapter = new InMemoryNotificationAdapter();
        var intent = TenantNotificationIntent.Create(owner, DurableWorkTestSupport.TenantWideScope(owner), VerifiedRecipient(owner.TenantId), "welcome", "en", "notify-h93-1");

        var result = await adapter.DeliverAsync(CreateContext(), intent);

        Assert.False(result.Delivered);
        Assert.False(result.Duplicate);
        Assert.Equal(DurableWorkFailureCategory.TenantMismatch, result.FailureCategory);
        Assert.Equal("tenant_denied", result.SafeOutcome);
    }

    [Fact]
    public async Task Wrong_tenant_delivery_leaves_delivery_state_attempt_count_and_failure_category_unchanged()
    {
        var owner = CreateContext();
        var adapter = new InMemoryNotificationAdapter();
        var intent = TenantNotificationIntent.Create(owner, DurableWorkTestSupport.TenantWideScope(owner), VerifiedRecipient(owner.TenantId), "welcome", "en", "notify-h93-2");

        await adapter.DeliverAsync(CreateContext(), intent);

        Assert.Equal(NotificationDeliveryState.Pending, intent.DeliveryState);
        Assert.Equal(0, intent.AttemptCount);
        Assert.Equal(DurableWorkFailureCategory.None, intent.FailureCategory);
    }

    [Fact]
    public async Task Wrong_tenant_delivery_does_not_consume_the_idempotency_key()
    {
        var owner = CreateContext();
        var adapter = new InMemoryNotificationAdapter();
        var intent = TenantNotificationIntent.Create(owner, DurableWorkTestSupport.TenantWideScope(owner), VerifiedRecipient(owner.TenantId), "welcome", "en", "notify-h93-3");

        await adapter.DeliverAsync(CreateContext(), intent);
        var ownerResult = await adapter.DeliverAsync(owner, intent);

        Assert.True(ownerResult.Delivered);
        Assert.False(ownerResult.Duplicate);
    }

    [Fact]
    public async Task Legitimate_owner_delivery_succeeds_after_a_wrong_tenant_attempt()
    {
        var owner = CreateContext();
        var adapter = new InMemoryNotificationAdapter();
        var intent = TenantNotificationIntent.Create(owner, DurableWorkTestSupport.TenantWideScope(owner), VerifiedRecipient(owner.TenantId), "welcome", "en", "notify-h93-4");

        await adapter.DeliverAsync(CreateContext(), intent);
        await adapter.DeliverAsync(CreateContext(), intent);
        var ownerResult = await adapter.DeliverAsync(owner, intent);

        Assert.True(ownerResult.Delivered);
        Assert.Equal(NotificationDeliveryState.Delivered, intent.DeliveryState);
        Assert.Equal(1, intent.AttemptCount);

        // A duplicate legitimately updates DeliveryState to Duplicate -- that
        // is expected M-7 behavior and asserted separately, after the
        // above Delivered-state assertions.
        var duplicateAfter = await adapter.DeliverAsync(owner, intent);
        Assert.True(duplicateAfter.Duplicate);
    }

    [Fact]
    public async Task Concurrent_wrong_tenant_calls_do_not_mutate_the_intent()
    {
        var owner = CreateContext();
        var adapter = new InMemoryNotificationAdapter();
        var intent = TenantNotificationIntent.Create(owner, DurableWorkTestSupport.TenantWideScope(owner), VerifiedRecipient(owner.TenantId), "welcome", "en", "notify-h93-5");

        var results = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => adapter.DeliverAsync(CreateContext(), intent).AsTask()));

        Assert.All(results, result => Assert.False(result.Delivered));
        Assert.Equal(NotificationDeliveryState.Pending, intent.DeliveryState);
        Assert.Equal(0, intent.AttemptCount);
    }

    [Fact]
    public async Task Concurrent_wrong_tenant_and_legitimate_owner_calls_produce_exactly_one_legitimate_effect()
    {
        var owner = CreateContext();
        var adapter = new InMemoryNotificationAdapter();
        var intent = TenantNotificationIntent.Create(owner, DurableWorkTestSupport.TenantWideScope(owner), VerifiedRecipient(owner.TenantId), "welcome", "en", "notify-h93-6");

        var callers = Enumerable.Range(0, 8).Select(index => index % 2 == 0 ? owner : CreateContext());
        var results = await Task.WhenAll(callers.Select(caller => adapter.DeliverAsync(caller, intent).AsTask()));

        // Exactly one of the four legitimate owner calls is the genuine
        // delivery; the other three legitimately observe Duplicate, and the
        // four wrong-Tenant calls never touch delivery state at all. Which
        // owner call wins the lock is nondeterministic, so DeliveryState's
        // final value (Delivered or Duplicate) is not asserted here -- the
        // single-effect guarantee is proven by the counts below instead.
        Assert.Single(results, result => result.Delivered && !result.Duplicate);
        Assert.Equal(3, results.Count(result => result.Duplicate));
        Assert.Equal(4, results.Count(result => !result.Delivered && !result.Duplicate));
        Assert.Equal(1, intent.AttemptCount);
    }

    // -------------------------------------------------------------------
    // M-8: notification recipient must be Identity-verified for the Tenant
    // -------------------------------------------------------------------

    [Fact]
    public async Task Active_recipient_with_membership_in_the_exact_tenant_is_authorized()
    {
        var service = new IdentityAuthorizationService();
        var tenant = new TenantId(Guid.NewGuid());
        var userId = service.CreateUser($"recipient-{Guid.NewGuid():N}@example.com", "Sup3r$ecret!1");
        service.AddMembership(userId, tenant);
        var caller = CreateAuthorizedMembershipCaller(service, tenant);

        var result = await service.AuthorizeAsync(caller, new NotificationRecipientReference(userId.Value));

        Assert.True(result.Allowed);
        Assert.Equal(tenant, result.Recipient!.TenantId);
        Assert.Equal(userId.Value, result.Recipient.UserId);
    }

    [Fact]
    public async Task Recipient_belonging_to_a_foreign_tenant_is_denied()
    {
        var service = new IdentityAuthorizationService();
        var ownTenant = new TenantId(Guid.NewGuid());
        var foreignTenant = new TenantId(Guid.NewGuid());
        var userId = service.CreateUser($"recipient-{Guid.NewGuid():N}@example.com", "Sup3r$ecret!1");
        service.AddMembership(userId, foreignTenant);
        var caller = CreateAuthorizedMembershipCaller(service, ownTenant);

        var result = await service.AuthorizeAsync(caller, new NotificationRecipientReference(userId.Value));

        Assert.False(result.Allowed);
        Assert.Null(result.Recipient);
    }

    [Fact]
    public async Task Unknown_recipient_is_denied()
    {
        var service = new IdentityAuthorizationService();
        var tenant = new TenantId(Guid.NewGuid());
        var caller = CreateAuthorizedMembershipCaller(service, tenant);

        var result = await service.AuthorizeAsync(caller, new NotificationRecipientReference(Guid.NewGuid()));

        Assert.False(result.Allowed);
    }

    [Fact]
    public async Task Suspended_global_user_recipient_is_denied()
    {
        var service = new IdentityAuthorizationService();
        var tenant = new TenantId(Guid.NewGuid());
        var userId = service.CreateUser($"recipient-{Guid.NewGuid():N}@example.com", "Sup3r$ecret!1");
        service.AddMembership(userId, tenant);
        service.Store.Users[userId].Status = GlobalUserStatus.Suspended;
        var caller = CreateAuthorizedMembershipCaller(service, tenant);

        var result = await service.AuthorizeAsync(caller, new NotificationRecipientReference(userId.Value));

        Assert.False(result.Allowed);
    }

    [Fact]
    public async Task Suspended_membership_recipient_is_denied() =>
        await AssertNonActiveMembershipDeniedAsync(MembershipStatus.Suspended);

    [Fact]
    public async Task Revoked_membership_recipient_is_denied() =>
        await AssertNonActiveMembershipDeniedAsync(MembershipStatus.Revoked);

    [Fact]
    public async Task Pending_invitation_membership_recipient_is_denied() =>
        await AssertNonActiveMembershipDeniedAsync(MembershipStatus.PendingInvitation);

    private static async Task AssertNonActiveMembershipDeniedAsync(MembershipStatus status)
    {
        var service = new IdentityAuthorizationService();
        var tenant = new TenantId(Guid.NewGuid());
        var userId = service.CreateUser($"recipient-{Guid.NewGuid():N}@example.com", "Sup3r$ecret!1");
        var membershipId = service.AddMembership(userId, tenant);
        service.Store.Memberships[membershipId].Status = status;
        var caller = CreateAuthorizedMembershipCaller(service, tenant);

        var result = await service.AuthorizeAsync(caller, new NotificationRecipientReference(userId.Value));

        Assert.False(result.Allowed);
    }

    [Fact]
    public void Notification_recipient_authorizer_requires_a_trusted_tenant_context_not_platform_governance()
    {
        var parameter = typeof(INotificationRecipientAuthorizer)
            .GetMethod(nameof(INotificationRecipientAuthorizer.AuthorizeAsync))!
            .GetParameters()[0];

        Assert.Equal(typeof(TenantContext), parameter.ParameterType);
        Assert.NotEqual(typeof(PlatformGovernanceContext), parameter.ParameterType);
    }

    // -------------------------------------------------------------------
    // H93-02: caller authority is live-revalidated, not trusted from the
    // structurally valid TenantContext alone
    // -------------------------------------------------------------------

    [Fact]
    public async Task Caller_with_a_fabricated_unregistered_membership_is_denied()
    {
        var service = new IdentityAuthorizationService();
        var tenant = new TenantId(Guid.NewGuid());
        var recipientUserId = service.CreateUser($"recipient-{Guid.NewGuid():N}@example.com", "Sup3r$ecret!1");
        service.AddMembership(recipientUserId, tenant);
        var fabricatedCaller = FabricatedCallerContext(tenant);

        var result = await service.AuthorizeAsync(fabricatedCaller, new NotificationRecipientReference(recipientUserId.Value));

        Assert.False(result.Allowed);
    }

    [Fact]
    public async Task Caller_whose_actor_is_suspended_is_denied()
    {
        var service = new IdentityAuthorizationService();
        var tenant = new TenantId(Guid.NewGuid());
        var recipientUserId = service.CreateUser($"recipient-{Guid.NewGuid():N}@example.com", "Sup3r$ecret!1");
        service.AddMembership(recipientUserId, tenant);
        var caller = CreateAuthorizedMembershipCaller(service, tenant, out var callerActorId);
        service.Store.Users[new UserId(callerActorId)].Status = GlobalUserStatus.Suspended;

        var result = await service.AuthorizeAsync(caller, new NotificationRecipientReference(recipientUserId.Value));

        Assert.False(result.Allowed);
    }

    [Fact]
    public async Task Caller_with_suspended_membership_is_denied() =>
        await AssertCallerMembershipDeniedAsync(MembershipStatus.Suspended);

    [Fact]
    public async Task Caller_with_revoked_membership_is_denied() =>
        await AssertCallerMembershipDeniedAsync(MembershipStatus.Revoked);

    [Fact]
    public async Task Caller_with_pending_invitation_membership_is_denied() =>
        await AssertCallerMembershipDeniedAsync(MembershipStatus.PendingInvitation);

    private static async Task AssertCallerMembershipDeniedAsync(MembershipStatus status)
    {
        var service = new IdentityAuthorizationService();
        var tenant = new TenantId(Guid.NewGuid());
        var recipientUserId = service.CreateUser($"recipient-{Guid.NewGuid():N}@example.com", "Sup3r$ecret!1");
        service.AddMembership(recipientUserId, tenant);
        var caller = CreateAuthorizedMembershipCaller(service, tenant);
        var callerMembershipId = new MembershipId(caller.Membership!.Value.Value);
        service.Store.Memberships[callerMembershipId].Status = status;

        var result = await service.AuthorizeAsync(caller, new NotificationRecipientReference(recipientUserId.Value));

        Assert.False(result.Allowed);
    }

    [Fact]
    public async Task Caller_membership_belonging_to_a_different_tenant_is_denied()
    {
        var service = new IdentityAuthorizationService();
        var callerHomeTenant = new TenantId(Guid.NewGuid());
        var targetTenant = new TenantId(Guid.NewGuid());
        var recipientUserId = service.CreateUser($"recipient-{Guid.NewGuid():N}@example.com", "Sup3r$ecret!1");
        service.AddMembership(recipientUserId, targetTenant);
        // The caller has a real, active Membership -- but in a different
        // Tenant than the one it claims to act for.
        var caller = CreateAuthorizedMembershipCaller(service, callerHomeTenant);
        var mismatchedCaller = TenantContext.ForOrdinaryMembership(
            targetTenant,
            caller.Membership!.Value,
            caller.Scope,
            caller.CorrelationId,
            caller.ActorId);

        var result = await service.AuthorizeAsync(mismatchedCaller, new NotificationRecipientReference(recipientUserId.Value));

        Assert.False(result.Allowed);
    }

    [Fact]
    public async Task Active_support_grant_caller_is_authorized()
    {
        var service = new IdentityAuthorizationService();
        var tenant = new TenantId(Guid.NewGuid());
        var recipientUserId = service.CreateUser($"recipient-{Guid.NewGuid():N}@example.com", "Sup3r$ecret!1");
        service.AddMembership(recipientUserId, tenant);
        var caller = CreateAuthorizedSupportCaller(service, tenant, TimeSpan.FromHours(1), out _, out _);

        var result = await service.AuthorizeAsync(caller, new NotificationRecipientReference(recipientUserId.Value));

        Assert.True(result.Allowed);
    }

    [Fact]
    public async Task Expired_support_grant_caller_is_denied()
    {
        var service = new IdentityAuthorizationService();
        var tenant = new TenantId(Guid.NewGuid());
        var recipientUserId = service.CreateUser($"recipient-{Guid.NewGuid():N}@example.com", "Sup3r$ecret!1");
        service.AddMembership(recipientUserId, tenant);
        var caller = CreateAuthorizedSupportCaller(service, tenant, TimeSpan.FromSeconds(-1), out _, out _);

        var result = await service.AuthorizeAsync(caller, new NotificationRecipientReference(recipientUserId.Value));

        Assert.False(result.Allowed);
    }

    [Fact]
    public async Task Revoked_support_grant_caller_is_denied()
    {
        var service = new IdentityAuthorizationService();
        var tenant = new TenantId(Guid.NewGuid());
        var recipientUserId = service.CreateUser($"recipient-{Guid.NewGuid():N}@example.com", "Sup3r$ecret!1");
        service.AddMembership(recipientUserId, tenant);
        var caller = CreateAuthorizedSupportCaller(service, tenant, TimeSpan.FromHours(1), out var grantId, out _);
        service.RevokeSupportGrant(grantId);

        var result = await service.AuthorizeAsync(caller, new NotificationRecipientReference(recipientUserId.Value));

        Assert.False(result.Allowed);
    }

    [Fact]
    public async Task Support_grant_caller_with_a_closed_case_is_denied()
    {
        var service = new IdentityAuthorizationService();
        var tenant = new TenantId(Guid.NewGuid());
        var recipientUserId = service.CreateUser($"recipient-{Guid.NewGuid():N}@example.com", "Sup3r$ecret!1");
        service.AddMembership(recipientUserId, tenant);
        var caller = CreateAuthorizedSupportCaller(service, tenant, TimeSpan.FromHours(1), out _, out var caseId);
        service.CloseSupportCase(caseId);

        var result = await service.AuthorizeAsync(caller, new NotificationRecipientReference(recipientUserId.Value));

        Assert.False(result.Allowed);
    }

    // -------------------------------------------------------------------
    // M93-01: INotificationRecipientAuthorizer must be resolvable from the
    // shipping Identity service collection, using the same Identity
    // authority/store instance as every other Identity-owned port
    // -------------------------------------------------------------------

    [Fact]
    public void Production_registration_resolves_notification_recipient_authorizer()
    {
        var services = new ServiceCollection();
        services.AddIdentityAuthorization();
        using var provider = services.BuildServiceProvider();

        var authorizer = provider.GetRequiredService<INotificationRecipientAuthorizer>();

        Assert.NotNull(authorizer);
    }

    [Fact]
    public void Production_registration_uses_the_same_identity_authority_instance()
    {
        var services = new ServiceCollection();
        services.AddIdentityAuthorization();
        using var provider = services.BuildServiceProvider();

        var authorizer = provider.GetRequiredService<INotificationRecipientAuthorizer>();
        var identityService = provider.GetRequiredService<IdentityAuthorizationService>();
        var reconciliationAuthorizer = provider.GetRequiredService<IDurableWorkAuthorityRevalidator>();

        Assert.Same(identityService, authorizer);
        Assert.Same(identityService, reconciliationAuthorizer);
    }

    [Fact]
    public void Production_registration_does_not_create_a_second_identity_store()
    {
        var services = new ServiceCollection();
        services.AddIdentityAuthorization();
        using var provider = services.BuildServiceProvider();

        var storeA = provider.GetRequiredService<IdentityStore>();
        var storeB = provider.GetRequiredService<IdentityStore>();

        Assert.Same(storeA, storeB);
    }

    // -------------------------------------------------------------------
    // helpers
    // -------------------------------------------------------------------

    private static TenantContext CreateContext(TenantId? tenantId = null) =>
        TenantContext.ForOrdinaryMembership(
            tenantId ?? new TenantId(Guid.NewGuid()),
            new MembershipReference(Guid.NewGuid()),
            new ScopeReference("tenant"),
            new CorrelationId($"corr-{Guid.NewGuid():N}"),
            Guid.NewGuid());

    /// <summary>Fabricated context whose Membership reference is never registered -- used only to prove H93-02 caller revalidation rejects it.</summary>
    private static TenantContext FabricatedCallerContext(TenantId tenantId) =>
        TenantContext.ForOrdinaryMembership(
            tenantId,
            new MembershipReference(Guid.NewGuid()),
            new ScopeReference("tenant"),
            new CorrelationId($"corr-{Guid.NewGuid():N}"),
            Guid.NewGuid());

    /// <summary>Creates a real, live actor with an active Membership in the given Tenant and returns its authorization context.</summary>
    private static TenantContext CreateAuthorizedMembershipCaller(IdentityAuthorizationService service, TenantId tenant) =>
        CreateAuthorizedMembershipCaller(service, tenant, out _);

    private static TenantContext CreateAuthorizedMembershipCaller(IdentityAuthorizationService service, TenantId tenant, out Guid callerActorId)
    {
        var actorId = service.CreateUser($"caller-{Guid.NewGuid():N}@example.com", "Sup3r$ecret!1");
        var membershipId = service.AddMembership(actorId, tenant);
        callerActorId = actorId.Value;
        return TenantContext.ForOrdinaryMembership(
            tenant,
            new MembershipReference(membershipId.Value),
            new ScopeReference("tenant"),
            new CorrelationId($"corr-{Guid.NewGuid():N}"),
            actorId.Value);
    }

    /// <summary>Creates a real, live actor with an active SupportGrant/SupportCase for the given Tenant and returns its authorization context.</summary>
    private static TenantContext CreateAuthorizedSupportCaller(
        IdentityAuthorizationService service,
        TenantId tenant,
        TimeSpan lifetime,
        out SupportGrantId grantId,
        out SupportCaseId caseId)
    {
        var actorId = service.CreateUser($"support-{Guid.NewGuid():N}@example.com", "Sup3r$ecret!1");
        var approverId = service.CreateUser($"approver-{Guid.NewGuid():N}@example.com", "Sup3r$ecret!1");
        caseId = service.AddSupportCase(tenant);
        grantId = new SupportGrantId(Guid.NewGuid());
        service.Store.SupportGrants.Add(
            grantId,
            new SupportGrant(
                grantId,
                caseId,
                actorId,
                approverId,
                tenant,
                "notification-test",
                OrganizationScope.ForTenant(tenant),
                [],
                DateTimeOffset.UtcNow.Add(lifetime)));
        return TenantContext.ForSupportGrant(
            tenant,
            new SupportGrantReference(grantId.Value, caseId.Value),
            new ScopeReference("tenant"),
            new CorrelationId($"corr-{Guid.NewGuid():N}"),
            actorId.Value);
    }

    private static VerifiedNotificationRecipient VerifiedRecipient(TenantId tenantId) =>
        new(tenantId, Guid.NewGuid());

    private static MemoryStream Content(string value) =>
        new(Encoding.UTF8.GetBytes(value));
}
