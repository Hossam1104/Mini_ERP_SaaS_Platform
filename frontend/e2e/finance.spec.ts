import { test, expect, type Page } from '@playwright/test';

const session = {
  authenticated: true,
  actorId: 'actor-1',
  sessionId: 'session-1',
  lifecycleState: 'Active',
  absoluteExpiresAt: null,
  selectedPath: 'OrdinaryMembership',
  selectedTenantId: 'tenant-a',
  selectedContextId: 'context-a',
  selectionVersion: 1,
};

async function setupFinanceRoutes(page: Page): Promise<void> {
  await page.route('**/api/v1/auth/development-bypass', (route) => route.fulfill({ json: { authenticated: false } }));
  await page.route('**/api/v1/auth/antiforgery', (route) => route.fulfill({ headers: { 'X-CSRF-TOKEN': 'finance-playwright-token' }, json: { status: 'issued' } }));
  await page.route('**/api/v1/auth/session', (route) => route.fulfill({ json: session }));
  await page.route('**/api/v1/auth/contexts', (route) => route.fulfill({ json: { contexts: [] } }));
  await page.route('**/api/v1/auth/entry', (route) => route.fulfill({ json: {
    entryMode: 'TenantHost',
    canonicalHost: '127.0.0.1',
    candidateTenantId: 'tenant-a',
    candidateTenantDisplayName: 'Alpha Tenant',
    authorizedTenants: [{ tenantId: 'tenant-a', displayName: 'Alpha Tenant', canonicalHost: 'tenant.localhost' }],
    operationalContexts: [{ contextId: 'context-a', kind: 'Company', displayName: 'Alpha Company', eligibilityVersion: 1 }],
    selectedOperationalContextId: 'context-a',
    operationalSelectionVersion: 1,
    branding: { displayName: 'Alpha Tenant', logoLightUrl: null, logoDarkUrl: null, logoAltText: 'Alpha Tenant', tenantConfigured: true },
    currencyPresentation: { currencyCode: 'SAR', symbolAssetUrl: null, symbolTextFallback: 'SAR' },
    code: null,
  } }));
  await page.route('**/api/v1/finance/companies', (route) => route.fulfill({ json: [{ tenantId: 'tenant-a', companyId: 'company-a', companyName: 'Alpha Company', functionalCurrencyCode: 'SAR', branchId: null, isActive: true }] }));
  await page.route('**/api/v1/master-data/currencies', (route) => route.fulfill({ json: [
    { id: 'currency-sar', tenantId: 'tenant-a', lifecycleState: 'Active', version: 'AQ==', code: 'SAR', englishName: 'Saudi Riyal', arabicName: null, revision: 1 },
    { id: 'currency-usd', tenantId: 'tenant-a', lifecycleState: 'Active', version: 'AQ==', code: 'USD', englishName: 'US Dollar', arabicName: null, revision: 1 },
  ] }));
  await page.route('**/api/v1/master-data/exchange-rates', (route) => route.fulfill({ json: [{
    id: 'rate-usd-sar', tenantId: 'tenant-a', lifecycleState: 'Active', version: 'AQ==',
    sourceCurrencyId: 'currency-usd', targetCurrencyId: 'currency-sar', sourceCurrencyCode: 'USD', targetCurrencyCode: 'SAR',
    currentVersionNumber: 3, versions: [{ id: 'rate-version-3', versionNumber: 3, effectiveFrom: '2026-01-01', effectiveTo: null, rate: 3.75, rateScale: 6, provenance: 'Configured', sourceNotes: 'MESP-120 configured reference', sourceCurrencyCode: 'USD', targetCurrencyCode: 'SAR' }],
  }] }));
  await page.route('**/api/v1/master-data/exchange-rates/rate-usd-sar/reference?effectiveOn=*', (route) => {
    const effectiveOn = new URL(route.request().url()).searchParams.get('effectiveOn') ?? '2026-08-25';
    return route.fulfill({ json: {
      id: 'rate-usd-sar', tenantId: 'tenant-a', sourceCurrencyId: 'currency-usd', targetCurrencyId: 'currency-sar', sourceCurrencyCode: 'USD', targetCurrencyCode: 'SAR',
      lifecycleState: 'Active', versionNumber: 3, versionId: 'rate-version-3', effectiveOn, effectiveFrom: '2026-01-01', effectiveTo: null,
      rate: 3.75, rateScale: 6, provenance: 'Configured', sourceNotes: 'MESP-120 configured reference', referenceValue: '3.750000', version: 'AQ==',
    } });
  });
  await page.route('**/api/v1/master-data/customers', (route) => route.fulfill({ json: [{ id: 'customer-a', tenantId: 'tenant-a', lifecycleState: 'Active', version: 'AQ==', code: 'CUS-1', englishLegalName: 'Customer One', arabicLegalName: null, englishTradingName: null, arabicTradingName: null, contacts: [] }] }));
  await page.route('**/api/v1/master-data/payment-terms', (route) => route.fulfill({ json: [{
    id: 'term-a', tenantId: 'tenant-a', lifecycleState: 'Active', version: 'AQ==', code: 'NET30', englishName: 'Net 30', arabicName: null, currentVersionNumber: 1,
    versions: [{ id: 'term-version-a', versionNumber: 1, effectiveFrom: '2026-01-01', effectiveTo: null, baseDateRule: 'DocumentDate', scheduleMode: 'SingleDueDate', dueOffsetDays: 30, dueOffsetMonths: 0, installments: [], earlySettlementDiscountEnabled: false, earlySettlementDiscountPercentage: null, earlySettlementDiscountDays: 0, earlySettlementDiscountMonths: 0, code: 'NET30', englishName: 'Net 30', arabicName: null }],
  }] }));
  await page.route('**/api/v1/finance/accounts**', (route) => route.fulfill({ json: [{ id: 'account-a', companyId: 'company-a', code: '1000', englishName: 'Cash', arabicName: null, parentAccountId: null, accountType: 'Asset', isPostingAccount: true, lifecycle: 'Active', effectiveFrom: '2026-01-01', effectiveTo: null, version: 'AQ==' }] }));
  await page.route('**/api/v1/finance/calendars**', (route) => route.fulfill({ json: [{ id: 'calendar-a', companyId: 'company-a', name: 'FY 2026', functionalCurrencyCode: 'SAR', lifecycle: 'Active', version: 'AQ==' }] }));
  await page.route('**/api/v1/finance/calendars/calendar-a/years', (route) => route.fulfill({ json: [{ id: 'year-a', calendarId: 'calendar-a', yearNumber: 2026, startDate: '2026-01-01', endDate: '2026-12-31', state: 'Open' }] }));
  await page.route('**/api/v1/finance/years/year-a/periods', (route) => route.fulfill({ json: [{ id: 'period-a', fiscalYearId: 'year-a', sequence: 1, code: '2026-01', englishName: 'January', arabicName: null, startDate: '2026-01-01', endDate: '2026-01-31', state: 'Open', version: 'AQ==' }] }));
  await page.route('**/api/v1/finance/posting-rules**', (route) => route.fulfill({ json: [] }));
  await page.route('**/api/v1/finance/journals**', (route) => route.fulfill({ json: [] }));
  await page.route('**/api/v1/finance/gl**', (route) => route.fulfill({ json: [] }));
  await page.route('**/api/v1/finance/inventory-handoffs**', (route) => route.fulfill({ json: [] }));
  await page.route('**/api/v1/finance/ap/open-items**', (route) => route.fulfill({ json: [{ id: 'open-item-a', companyId: 'company-a', kind: 'Payable', partyId: 'supplier-a', reference: 'PI-1001', documentDate: '2026-08-01', dueDate: '2026-08-31', currencyCode: 'SAR', originalAmount: 1250, allocatedAmount: 0, outstandingAmount: 1250, sourceContract: 'procurement-supplier-invoice.v1', sourceIdentity: 'match-a', recognitionState: 'Recognized', status: 'Open', recognitionJournalId: 'journal-a', version: 'AQ==' }] }));
  await page.route('**/api/v1/finance/ap/source-ready**', (route) => route.fulfill({ json: [{ sourceEvidenceId: 'source-a', companyId: 'company-a', supplierId: 'supplier-a', supplierCode: 'SUP-1', supplierName: 'Supplier One', supplierInvoiceReference: 'PI-READY-1', invoiceDate: '2026-08-01', currencyCode: 'SAR', amount: 1250, dueDate: '2026-08-31', paymentTerm: { code: 'NET30', englishName: 'Net 30', arabicName: null, versionNumber: 1, dueDate: '2026-08-31' }, matchResult: 'ExactMatch', alreadyRecognized: false, sourceEvidenceVersion: 1 }] }));
  await page.route('**/api/v1/finance/ap/recognize', async (route) => { expect(route.request().method()).toBe('POST'); await route.fulfill({ json: { id: 'open-item-a', companyId: 'company-a', kind: 'Payable', reference: 'PI-READY-1', documentDate: '2026-08-01', dueDate: '2026-08-31', currencyCode: 'SAR', originalAmount: 1250, allocatedAmount: 0, outstandingAmount: 1250, status: 'Open', recognitionState: 'Recognized', recognitionJournalId: 'journal-a', version: 'AQ==' } }); });
  await page.route('**/api/v1/finance/ap/aging**', (route) => route.fulfill({ json: [{ openItemId: 'open-item-a', reference: 'PI-1001', dueDate: '2026-08-31', daysOverdue: 0, outstandingAmount: 1250, currencyCode: 'SAR', status: 'Open' }] }));
  await page.route('**/api/v1/finance/ar/open-items**', (route) => route.fulfill({ json: [] }));
  await page.route('**/api/v1/finance/ar/aging**', (route) => route.fulfill({ json: [] }));
  await page.route('**/api/v1/finance/payment-methods**', (route) => route.fulfill({ json: [{ id: 'method-a', companyId: 'company-a', code: 'BANK', name: 'Bank transfer', direction: 'Both', lifecycle: 'Active', version: 'AQ==' }] }));
  await page.route('**/api/v1/finance/cash-accounts**', (route) => route.fulfill({ json: [{ id: 'cash-a', companyId: 'company-a', code: '1000', name: 'Main bank', kind: 'Bank', currencyCode: 'SAR', linkedAccountCode: '1000', lifecycle: 'Active', version: 'AQ==' }] }));
  await page.route('**/api/v1/finance/payments**', (route) => route.fulfill({ json: [] }));
  await page.route('**/api/v1/finance/receipts**', (route) => route.fulfill({ json: [] }));
  await page.route('**/api/v1/finance/allocations**', (route) => route.fulfill({ json: [] }));
  await page.route('**/api/v1/finance/settlement/reconciliation**', (route) => route.fulfill({ json: [{ scope: 'Settlement', subledgerAmount: 0, postedJournalAmount: 0, difference: 0, status: 'Reconciled' }] }));
}

async function setupMesp135Routes(page: Page, initialPeriodState = 'Open'): Promise<void> {
  await setupFinanceRoutes(page);
  let periodState = initialPeriodState;
  let closeRuns: Record<string, unknown>[] = [];
  let closeHistory: Record<string, unknown>[] = [];
  let yearEndRun: Record<string, unknown> | null = null;
  const readiness = { periodId: 'period-a', fiscalYearId: 'year-a', tenantId: 'tenant-a', companyId: 'company-a', status: 'Ready', checks: [{ code: 'gl_balanced', status: 'Ready', message: 'Posted functional totals are balanced.' }], snapshotFingerprint: 'm135-close-fingerprint', evaluatedAt: '2026-08-26T00:00:00Z', periodVersion: 'AQ==' };

  await page.route('**/api/v1/finance/years/year-a/periods', (route) => route.fulfill({ json: [{ id: 'period-a', fiscalYearId: 'year-a', sequence: 1, code: '2026-01', englishName: 'January', arabicName: null, startDate: '2026-01-01', endDate: '2026-01-31', state: periodState, version: periodState === 'Open' ? 'AQ==' : 'Ag==' }] }));
  await page.route(/\/api\/v1\/finance\/periods\/period-a\/close-readiness(?:\?.*)?$/, (route) => route.fulfill({ json: readiness }));
  await page.route(/\/api\/v1\/finance\/period-close-runs(?:\?.*)?$/, (route) => route.fulfill({ json: closeRuns }));
  await page.route(/\/api\/v1\/finance\/periods\/period-a\/close-history(?:\?.*)?$/, (route) => route.fulfill({ json: closeHistory }));
  await page.route(/\/api\/v1\/finance\/reconciliation\/close(?:\?.*)?$/, (route) => route.fulfill({ json: { companyId: 'company-a', periodId: 'period-a', asOfDate: '2026-01-31', overallStatus: 'Reconciled', items: [{ companyId: 'company-a', asOfDate: '2026-01-31', scope: 'GL', status: 'Reconciled', expectedAmount: 100, actualAmount: 100, difference: 0, sourceReference: null, detail: 'Balanced posted facts.', hasDurableEvidence: true }], closeHistory, yearEndRuns: yearEndRun ? [yearEndRun] : [] } }));
  await page.route(/\/api\/v1\/finance\/year-end(?:\?.*)?$/, (route) => route.fulfill({ json: yearEndRun ? [yearEndRun] : [] }));
  await page.route(/\/api\/v1\/finance\/periods\/period-a\/close$/, async (route) => {
    periodState = 'Closed';
    const run = { id: `close-${closeRuns.length + 1}`, tenantId: 'tenant-a', companyId: 'company-a', fiscalYearId: 'year-a', periodId: 'period-a', sequence: closeRuns.length + 1, status: 'Closed', readinessStatus: 'Ready', snapshotFingerprint: 'm135-close-fingerprint', checks: readiness.checks, reason: closeRuns.length === 0 ? 'MESP-135 browser close' : 'MESP-135 browser reclose', actorId: 'actor-1', sessionId: 'session-1', correlationId: 'corr-close', createdAt: '2026-08-26T00:00:00Z', reopenedAt: null, reopenedBy: null, version: 'Ag==' };
    closeRuns = [...closeRuns, run];
    closeHistory = [...closeHistory, { id: `history-${closeRuns.length}`, tenantId: 'tenant-a', companyId: 'company-a', fiscalYearId: 'year-a', periodId: 'period-a', action: closeRuns.length === 1 ? 'Closed' : 'Reclosed', fromState: 'Open', toState: 'Closed', closeRunId: run.id, actorId: 'actor-1', sessionId: 'session-1', correlationId: 'corr-close', reason: run.reason, occurredAt: '2026-08-26T00:00:00Z' }];
    await route.fulfill({ json: run });
  });
  await page.route(/\/api\/v1\/finance\/periods\/period-a\/reopen$/, async (route) => {
    periodState = 'Open';
    const latest = closeRuns[closeRuns.length - 1];
    const run = { ...latest, status: 'Reopened', reopenedAt: '2026-08-26T00:01:00Z', reopenedBy: 'actor-1', version: 'Aw==' };
    closeRuns = closeRuns.map((item, index) => index === closeRuns.length - 1 ? run : item);
    closeHistory = [...closeHistory, { id: `history-${closeHistory.length + 1}`, tenantId: 'tenant-a', companyId: 'company-a', fiscalYearId: 'year-a', periodId: 'period-a', action: 'Reopened', fromState: 'Closed', toState: 'Open', closeRunId: (latest?.id as string) ?? null, actorId: 'actor-1', sessionId: 'session-1', correlationId: 'corr-reopen', reason: 'MESP-135 browser reopen', occurredAt: '2026-08-26T00:01:00Z' }];
    await route.fulfill({ json: run });
  });
  await page.route(/\/api\/v1\/finance\/year-end\/calculate$/, async (route) => {
    yearEndRun = { id: 'year-end-a', tenantId: 'tenant-a', companyId: 'company-a', fiscalYearId: 'year-a', asOfDate: '2026-12-31', status: 'Calculated', snapshotFingerprint: 'm135-year-end-fingerprint', lines: [{ id: 'line-a', runId: 'year-end-a', accountId: 'account-revenue', accountCode: '4000', accountName: 'Revenue', accountNameArabic: null, accountType: 'Revenue', debit: 100, credit: 0, netBalance: 100, closingJournalLineId: null }], closingJournalId: null, reversalJournalId: null, retainedEarningsAccountId: 'account-equity', retainedEarningsAccountCode: '3900', postingRuleId: 'rule-year-end', postingRuleVersionNumber: 1, postingRuleSourceContract: 'finance-year-end.v1', postingRuleSourceEvent: 'close', reason: 'MESP-135 browser year-end', actorId: 'actor-1', sessionId: 'session-1', correlationId: 'corr-year-end', createdAt: '2026-08-26T00:00:00Z', postedAt: null, reversedAt: null, version: 'AQ==' };
    await route.fulfill({ json: yearEndRun });
  });
  await page.route(/\/api\/v1\/finance\/year-end\/year-end-a\/(post|reverse)$/, async (route) => {
    const action = route.request().url().endsWith('/reverse') ? 'Reversed' : 'Posted';
    yearEndRun = { ...yearEndRun, status: action, closingJournalId: 'journal-year-end', reversalJournalId: action === 'Reversed' ? 'journal-year-end-reversal' : null, version: action === 'Posted' ? 'Ag==' : 'Aw==' };
    await route.fulfill({ json: yearEndRun });
  });

  const trial = { companyId: 'company-a', asOfDate: '2026-01-31', fromDate: '2026-01-01', toDate: '2026-01-31', rows: [{ accountId: 'account-cash', accountCode: '1000', accountName: 'Cash', accountNameArabic: null, accountType: 'Asset', openingBalance: 10, periodDebit: 100, periodCredit: 20, closingBalance: 90, functionalCurrencyCode: 'SAR', reportingCurrencyCode: null, reportingOpeningBalance: null, reportingDebit: null, reportingCredit: null, reportingClosingBalance: null, reportingEvidenceStatus: 'NotCaptured' }], totalDebit: 100, totalCredit: 100, totalClosingBalance: 90, functionalCurrencyCode: 'SAR', reportingCurrencyCode: null, reportingEvidenceStatus: 'NotCaptured' };
  await page.route(/\/api\/v1\/finance\/reports\/trial-balance(?:\?.*)?$/, (route) => route.fulfill({ json: trial }));
  await page.route(/\/api\/v1\/finance\/reports\/general-ledger(?:\?.*)?$/, (route) => route.fulfill({ json: [{ journalId: 'journal-a', journalNumber: 'J-1', journalSequence: 1, postingDate: '2026-01-15', accountId: 'account-cash', accountCode: '1000', accountName: 'Cash', accountNameArabic: null, fiscalPeriodId: 'period-a', costCenterId: null, costCenterCode: null, sourceContract: 'manual-journal.v1', sourceEvent: 'manual', sourceEvidenceId: null, lineNumber: 1, debit: 100, credit: 0, functionalDebit: 100, functionalCredit: 0, runningBalance: 100, functionalCurrencyCode: 'SAR', transactionCurrencyCode: 'SAR', transactionAmount: 100, reportingAmount: null, reportingEvidenceStatus: 'NotCaptured', isReversal: false }] }));
  await page.route(/\/api\/v1\/finance\/reports\/(ap|ar)-aging(?:\?.*)?$/, (route) => route.fulfill({ json: [{ openItemId: 'open-item-a', kind: route.request().url().includes('/ap-aging') ? 'Payable' : 'Receivable', supplierId: 'supplier-a', customerId: null, sourceReference: 'PI-1001', documentDate: '2026-01-01', dueDate: '2026-01-31', asOfDate: '2026-01-31', daysOverdue: 0, agingBucket: 'Current', currencyCode: 'SAR', originalAmount: 100, allocatedAmount: 20, outstandingAmount: 80, functionalCurrencyCode: 'SAR', originalFunctionalAmount: 100, outstandingFunctionalAmount: 80, status: 'Open' }] }));
  await page.route(/\/api\/v1\/finance\/reports\/(profit-loss|balance-sheet)(?:\?.*)?$/, (route) => route.fulfill({ json: { kind: route.request().url().includes('profit-loss') ? 'ProfitAndLoss' : 'BalanceSheet', companyId: 'company-a', fromDate: '2026-01-01', toDate: '2026-01-31', rows: trial.rows.map((row) => ({ ...row, debit: row.periodDebit, credit: row.periodCredit })), totalDebit: 100, totalCredit: 100, totalClosingBalance: 90, functionalCurrencyCode: 'SAR', finding: null } }));
  await page.route(/\/api\/v1\/finance\/reports\/(trial-balance|general-ledger|ap-aging|ar-aging)\/export(?:\?.*)?$/, (route) => route.fulfill({ status: 200, headers: { 'Content-Type': 'text/csv', 'Content-Disposition': 'attachment; filename=finance.csv' }, body: 'Account,Name\n1000,Cash\n' }));
}

async function setupTaxFxRoutes(page: Page): Promise<{ requests: Record<string, Record<string, unknown> | undefined> }> {
  await setupFinanceRoutes(page);
  const requests: Record<string, Record<string, unknown> | undefined> = {};
  const taxEffect = {
    id: 'tax-effect-a', companyId: 'company-a', openItemId: 'open-item-a', kind: 'Payable', taxId: 'tax-a', taxCode: 'VAT15',
    taxRateVersionId: 'tax-rate-a', taxRateVersionNumber: 1, taxEffectiveOn: '2026-01-01', taxRatePercentage: 15,
    taxableBase: 1000, taxAmount: 150, transactionCurrencyCode: 'SAR', functionalAmount: 150, functionalCurrencyCode: 'SAR',
    journalId: 'journal-tax-a', reversalJournalId: null, postingRuleId: 'rule-tax-a', postingRuleVersionNumber: 1,
    monetaryEvidence: { transactionCurrencyCode: 'SAR', transactionAmount: 150, functionalCurrencyCode: 'SAR', functionalAmount: 150, reportingCurrencyCode: 'USD', reportingAmount: 40, transactionToFunctionalRate: null, functionalToReportingRate: { id: 'rate-usd-sar', versionId: 'rate-version-3', versionNumber: 3, rate: 3.75 }, sourceUnroundedFunctionalAmount: 150, sourceUnroundedReportingAmount: 40, roundingScale: 2, roundingMode: 'AwayFromZero', functionalRoundingDifference: 0, reportingRoundingDifference: 0, reportingEvidenceStatus: 'Captured' },
    status: 'Posted', createdAt: '2026-08-26T00:00:00Z', version: 'AQ==',
  };
  const batch = { id: 'batch-a', companyId: 'company-a', asOfDate: '2026-08-25', scope: 'AP_AR_AND_UNALLOCATED_SETTLEMENTS', status: 'Draft', lines: [], version: 'AQ==' };
  let batchStatus = 'Draft';

  await page.route('**/api/v1/master-data/taxes**', (route) => route.fulfill({ json: [{ id: 'tax-a', tenantId: 'tenant-a', lifecycleState: 'Active', version: 'AQ==', code: 'VAT15', englishName: 'VAT 15%', arabicName: 'ضريبة 15%', currentVersionNumber: 1, versions: [{ id: 'tax-rate-a', versionNumber: 1, effectiveFrom: '2026-01-01', effectiveTo: null, ratePercentage: 15 }] }] }));
  await page.route('**/api/v1/finance/monetary-policy**', (route) => route.fulfill({ json: [{ id: 'policy-a', tenantId: 'tenant-a', companyId: 'company-a', functionalCurrencyCode: 'SAR', reportingCurrencyId: 'currency-usd', reportingCurrencyCode: 'USD', roundingScale: 2, roundingMode: 'AwayFromZero', revaluationEnabled: true, effectiveFrom: '2026-01-01', effectiveTo: null, versionNumber: 1, version: 'AQ==' }] }));
  await page.route('**/api/v1/finance/tax-accounting/preview', async (route) => { requests.taxPreview = route.request().postDataJSON() as Record<string, unknown>; await route.fulfill({ json: taxEffect }); });
  await page.route(/\/api\/v1\/finance\/tax-accounting(?:\?.*)?$/, async (route) => { if (route.request().method() === 'GET') return route.fulfill({ json: [taxEffect] }); requests.taxPost = route.request().postDataJSON() as Record<string, unknown>; await route.fulfill({ json: taxEffect }); });
  await page.route('**/api/v1/finance/tax-accounting/*/reverse', async (route) => { requests.taxReverse = route.request().postDataJSON() as Record<string, unknown>; await route.fulfill({ json: { ...taxEffect, reversalJournalId: 'journal-tax-reversal' } }); });
  await page.route('**/api/v1/finance/revaluation**', async (route) => {
    const method = route.request().method();
    if (method === 'GET') return route.fulfill({ json: [{ ...batch, status: batchStatus }] });
    if (method === 'POST') { requests.revaluationCreate = route.request().postDataJSON() as Record<string, unknown>; batchStatus = 'Draft'; return route.fulfill({ json: { ...batch, status: batchStatus } }); }
    return route.continue();
  });
  await page.route(/\/api\/v1\/finance\/revaluation\/[^/]+\/calculate$/, async (route) => { requests.revaluationCalculate = route.request().postDataJSON() as Record<string, unknown>; batchStatus = 'Calculated'; await route.fulfill({ json: { ...batch, status: batchStatus } }); });
  await page.route(/\/api\/v1\/finance\/revaluation\/[^/]+\/post$/, async (route) => { requests.revaluationPost = route.request().postDataJSON() as Record<string, unknown>; batchStatus = 'Posted'; await route.fulfill({ json: { ...batch, status: batchStatus } }); });
  await page.route(/\/api\/v1\/finance\/revaluation\/[^/]+\/reverse$/, async (route) => { requests.revaluationReverse = route.request().postDataJSON() as Record<string, unknown>; batchStatus = 'Reversed'; await route.fulfill({ json: { ...batch, status: batchStatus } }); });
  await page.route('**/api/v1/finance/fx-reconciliation**', (route) => route.fulfill({ json: [{ allocationId: 'allocation-a', companyId: 'company-a', realizedDifference: 12.5, postedDifference: 12.5, direction: 'Gain', status: 'Reconciled', journalId: 'journal-fx-a', openItemId: 'open-item-a', settlementDocumentId: null, reversalJournalId: null, expectedAccountId: 'account-fx', ruleId: 'rule-fx', ruleVersionNumber: 1, statusReason: null }] }));
  await page.route('**/api/v1/finance/unrealized-fx-reconciliation**', (route) => route.fulfill({ json: [{ lineId: 'line-a', batchId: 'batch-a', companyId: 'company-a', sourceId: 'open-item-a', sourceType: 'AR', expectedAmount: 8.25, postedAmount: 8.25, direction: 'Loss', status: 'Reconciled', journalId: 'journal-unrealized-a', reversalJournalId: null, expectedAccountId: 'account-fx', postingRuleId: 'rule-fx', postingRuleVersionNumber: 1, statusReason: null }] }));
  await page.route('**/api/v1/finance/reporting-currency-reconciliation**', (route) => route.fulfill({ json: [{ journalId: 'journal-tax-a', companyId: 'company-a', functionalCurrencyCode: 'SAR', functionalAmount: 150, reportingCurrencyCode: 'USD', reportingAmount: 40, expectedReportingAmount: 40, exchangeRateId: 'rate-usd-sar', exchangeRateVersionId: 'rate-version-3', exchangeRateVersionNumber: 3, status: 'Reconciled', effectId: 'tax-effect-a', statusReason: null }] }));
  return { requests };
}

test('Finance workspace renders Company books, periods, and bounded GL evidence', async ({ page }) => {
  await setupFinanceRoutes(page);
  await page.goto('/app/finance');

  const finance = page.locator('[data-testid="finance-workspace"]');
  await expect(finance).toBeVisible();
  await expect(finance.getByRole('heading', { level: 1 })).toContainText('Company books');
  await expect(finance.locator('select')).toContainText('Alpha Company');
  await expect(finance.getByRole('tab', { name: 'Chart of accounts' })).toBeVisible();
  await expect(finance.getByRole('tab', { name: 'GL inquiry' })).toBeVisible();
  await finance.getByRole('tab', { name: 'Chart of accounts' }).click();
  await expect(finance).toContainText('Cash');
});

test('Finance workspace changes the authenticated shell to RTL for Arabic', async ({ page }) => {
  await setupFinanceRoutes(page);
  await page.goto('/app/finance');
  await expect(page.locator('[data-testid="finance-workspace"]')).toBeVisible();
  await page.locator('.language-button').click();
  await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
});

test('AP workspace renders source lineage and aging evidence', async ({ page }) => {
  await setupFinanceRoutes(page);
  await page.goto('/app/finance/ap');

  const payable = page.locator('[data-testid="finance-settlement-workspace"]');
  await expect(payable).toBeVisible();
  await expect(payable.getByRole('heading', { level: 1 })).toContainText('Accounts Payable');
  await expect(payable).toContainText('PI-1001');
  await expect(payable).toContainText('procurement-supplier-invoice.v1');
  await expect(payable).toContainText('Aging');
});

test('AP workspace executes the bounded source-ready recognition journey', async ({ page }) => {
  await setupFinanceRoutes(page);
  await page.goto('/app/finance/ap');

  const payable = page.locator('[data-testid="finance-settlement-workspace"]');
  await expect(payable.locator('[data-testid="ap-source-ready"]')).toContainText('PI-READY-1');
  const sourceReadyGrid = payable.locator('[data-testid="ap-source-ready"] app-data-grid');
  await sourceReadyGrid.locator('.grid-row-menu-trigger').click();
  await sourceReadyGrid.getByRole('menuitem', { name: 'Recognize payable', exact: true }).click();
  await expect(payable).toContainText('PI-READY-1');
});

test('settlements workspace presents configured methods and on-account reconciliation', async ({ page }) => {
  await setupFinanceRoutes(page);
  await page.goto('/app/finance/settlements');

  const settlements = page.locator('[data-testid="finance-settlement-workspace"]');
  await expect(settlements).toBeVisible();
  await expect(settlements.getByRole('heading', { level: 1 })).toContainText('Payments, receipts, and allocation');
  await expect(settlements).toContainText('Payment Method');
  await expect(settlements).toContainText('Settlement');
});

test('Manual AR resolves exact MESP-120 non-functional currency evidence before creation', async ({ page }) => {
  await setupFinanceRoutes(page);
  let submittedPayload: Record<string, unknown> | undefined;
  await page.route('**/api/v1/finance/ar/manual', async (route) => {
    submittedPayload = route.request().postDataJSON() as Record<string, unknown>;
    await route.fulfill({ json: {
      id: 'ar-fx-1', companyId: 'company-a', customerId: 'customer-a', kind: 'Receivable', reference: 'AR-FX-1', documentDate: '2026-08-25', dueDate: '2026-09-24', currencyCode: 'USD', originalAmount: 100, allocatedAmount: 0, outstandingAmount: 100, status: 'Open', recognitionState: 'Recognized', recognitionJournalId: 'journal-ar-fx', version: 'AQ==',
    } });
  });

  await page.goto('/app/finance/ar');
  const ar = page.locator('[data-testid="finance-settlement-workspace"]');
  await ar.locator('select').first().selectOption('company-a');
  await ar.locator('[data-testid="ar-customer-select"]').selectOption('customer-a');
  await ar.locator('[data-testid="ar-payment-term-select"]').selectOption('term-a');
  await ar.locator('input').nth(1).fill('USD');
  await ar.locator('input[type="number"]').fill('100');
  await expect(ar.locator('[data-testid="ar-exchange-rate-select"]')).toBeVisible();
  await ar.locator('[data-testid="ar-exchange-rate-select"]').selectOption('rate-usd-sar');
  await expect(ar.locator('[data-testid="ar-fx-reference"]')).toContainText('3.75');
  const createReceivable = ar.getByRole('button', { name: 'Create manual receivable' });
  await expect(createReceivable).toBeEnabled();
  await createReceivable.click();
  await expect.poll(() => submittedPayload).toBeDefined();

  expect(submittedPayload).toMatchObject({
    companyId: 'company-a', customerId: 'customer-a', paymentTermId: 'term-a', currencyCode: 'USD', amount: 100,
    exchangeRate: 3.75, exchangeRateId: 'rate-usd-sar', exchangeRateVersionId: 'rate-version-3', exchangeRateVersionNumber: 3,
  });
});

test('Tax workspace previews, posts, and reverses with source and reason evidence', async ({ page }) => {
  const { requests } = await setupTaxFxRoutes(page);
  await page.goto('/app/finance/tax-fx');
  const workspace = page.locator('[data-testid="finance-tax-fx-workspace"]');
  await expect(workspace).toBeVisible();
  await workspace.getByRole('tab', { name: 'Tax accounting' }).click();
  await workspace.locator('select[name="source"]').selectOption('open-item-a');
  await workspace.locator('select[name="tax"]').selectOption('tax-a');
  await workspace.locator('input[name="base"]').fill('1000');
  await workspace.getByRole('button', { name: 'Calculate preview' }).click();
  await expect(workspace).toContainText('40 USD');
  await workspace.getByRole('button', { name: 'Post', exact: true }).click();
  await workspace.locator('input[name="taxReverseReason"]').fill('Corrected declared tax evidence');
  const taxEffectsGrid = workspace.locator('app-data-grid').last();
  await taxEffectsGrid.locator('.grid-row-menu-trigger').click();
  await taxEffectsGrid.getByRole('menuitem', { name: 'Reverse', exact: true }).click();
  await expect.poll(() => requests.taxPreview).toMatchObject({ companyId: 'company-a', openItemId: 'open-item-a', taxId: 'tax-a', taxableBase: 1000, sourceLineage: 'finance-tax-workspace' });
  expect(requests.taxPost).toMatchObject({ companyId: 'company-a', openItemId: 'open-item-a', taxId: 'tax-a', taxableBase: 1000, sourceLineage: 'finance-tax-workspace' });
  expect(requests.taxReverse).toEqual({ reason: 'Corrected declared tax evidence' });
});

test('Tax/FX workspace exposes realized, unrealized, and reporting reconciliation evidence', async ({ page }) => {
  await setupTaxFxRoutes(page);
  await page.goto('/app/finance/tax-fx');
  const workspace = page.locator('[data-testid="finance-tax-fx-workspace"]');
  await workspace.getByRole('tab', { name: 'Revaluation' }).click();
  await expect(workspace.locator('[data-testid="realized-fx-reconciliation"]')).toContainText('12.50');
  await expect(workspace.locator('[data-testid="realized-fx-reconciliation"]')).toContainText('journal-fx-a');
  await expect(workspace.locator('[data-testid="unrealized-fx-reconciliation"]')).toContainText('8.25');
  await expect(workspace.locator('[data-testid="reporting-currency-reconciliation"]')).toContainText('40 USD');
  await expect(workspace).toContainText('Reconciled');
});

test('Revaluation workspace runs the controlled draft, calculate, post, and reverse journey', async ({ page }) => {
  const { requests } = await setupTaxFxRoutes(page);
  await page.goto('/app/finance/tax-fx');
  const workspace = page.locator('[data-testid="finance-tax-fx-workspace"]');
  await workspace.getByRole('tab', { name: 'Revaluation' }).click();
  await expect(workspace.locator('select[name="scope"]')).toBeDisabled();
  await expect(workspace.locator('select[name="scope"]')).toHaveValue('AP_AR_AND_UNALLOCATED_SETTLEMENTS');
  await workspace.locator('input[name="asOfDate"]').fill('2026-08-25');
  await workspace.getByRole('button', { name: 'Create draft' }).click();
  await expect.poll(() => requests.revaluationCreate).toEqual({ companyId: 'company-a', asOfDate: '2026-08-25', scope: 'AP_AR_AND_UNALLOCATED_SETTLEMENTS' });
  await workspace.getByRole('button', { name: 'Calculate' }).click();
  await expect.poll(() => requests.revaluationCalculate).toEqual({});
  await workspace.getByRole('button', { name: 'Post', exact: true }).click();
  await expect.poll(() => requests.revaluationPost).toEqual({});
  await workspace.locator('input[name="revaluationReverseReason"]').fill('Re-run after source correction');
  await workspace.getByRole('button', { name: 'Reverse', exact: true }).click();
  await expect.poll(() => requests.revaluationReverse).toEqual({ reason: 'Re-run after source correction' });
});

test('MESP-135 close workspace evaluates, closes, reopens, and records reclose history', async ({ page }) => {
  await setupMesp135Routes(page);
  await page.goto('/app/finance/close');
  const workspace = page.locator('.close-page');
  await expect(workspace.getByRole('heading', { level: 1 })).toContainText('Close periods with evidence');
  await expect(workspace).toContainText('Ready');
  await workspace.locator('input.reason').fill('MESP-135 browser close');
  await workspace.getByRole('button', { name: 'Close period' }).click();
  await expect(workspace).toContainText('Closed');
  await workspace.locator('input.reason').fill('MESP-135 browser reopen');
  await workspace.getByRole('button', { name: 'Reopen period' }).click();
  await expect(workspace).toContainText('Reopened');
  await expect(workspace).toContainText('#1 · Reopened');
  await workspace.getByRole('button', { name: 'Close period' }).click();
  await expect(workspace).toContainText('#2 · Closed');
  await expect(workspace).toContainText('MESP-135 browser reclose');
});

test('MESP-135 year-end workspace calculates, posts, and reverses the configured run', async ({ page }) => {
  await setupMesp135Routes(page, 'Closed');
  await page.goto('/app/finance/close');
  const workspace = page.locator('.close-page');
  await expect(workspace).toContainText('Year-end runs');
  await workspace.getByRole('button', { name: 'Calculate year-end' }).click();
  await expect(workspace).toContainText('Calculated');
  await workspace.getByRole('button', { name: 'Post year-end' }).click();
  await expect(workspace).toContainText('Posted');
  await workspace.getByRole('button', { name: 'Reverse year-end' }).click();
  await expect(workspace).toContainText('Reversed');
  await expect(workspace).toContainText('3900');
});

test('MESP-135 reports query trial balance, ledger, statements, and authorized CSV export', async ({ page }) => {
  await setupMesp135Routes(page);
  let exportRequest = '';
  await page.route(/\/api\/v1\/finance\/reports\/trial-balance\/export(?:\?.*)?$/, async (route) => { exportRequest = route.request().url(); await route.fulfill({ status: 200, headers: { 'Content-Type': 'text/csv', 'Content-Disposition': 'attachment; filename=trial-balance.csv' }, body: 'Account,Name\n1000,Cash\n' }); });
  await page.goto('/app/finance/reports');
  const workspace = page.locator('.reports-page');
  await expect(workspace.getByRole('heading', { level: 1 })).toContainText('Core reports from posted facts');
  await expect(workspace).toContainText('Cash');
  await workspace.getByRole('link', { name: 'CSV export' }).click();
  await expect.poll(() => exportRequest).toContain('asOfDate=');
  await workspace.getByRole('tab', { name: 'General ledger' }).click();
  await expect(workspace).toContainText('J-1');
  await workspace.getByRole('tab', { name: 'Profit & loss' }).click();
  await expect(workspace).toContainText('Profit & loss');
  await workspace.getByRole('tab', { name: 'Balance sheet' }).click();
  await expect(workspace).toContainText('Balance sheet');
});

test('MESP-135 AP and AR aging keep the requested accounting as-of date', async ({ page }) => {
  await setupMesp135Routes(page);
  const requested: string[] = [];
  await page.route(/\/api\/v1\/finance\/reports\/(ap|ar)-aging(?:\?.*)?$/, async (route) => { requested.push(new URL(route.request().url()).searchParams.get('asOfDate') ?? ''); await route.fulfill({ json: [{ openItemId: 'open-item-a', sourceReference: 'PI-1001', dueDate: '2026-01-31', asOfDate: '2026-01-31', agingBucket: 'Current', currencyCode: 'SAR', originalAmount: 100, allocatedAmount: 20, outstandingAmount: 80, kind: route.request().url().includes('/ap-aging') ? 'Payable' : 'Receivable', status: 'Open' }] }); });
  await page.goto('/app/finance/reports');
  const workspace = page.locator('.reports-page');
  await workspace.locator('input[type="date"]').first().fill('2026-01-31');
  await workspace.getByRole('tab', { name: 'AP aging' }).click();
  await expect(workspace).toContainText('PI-1001');
  await workspace.getByRole('tab', { name: 'AR aging' }).click();
  await expect(workspace).toContainText('PI-1001');
  await expect.poll(() => requested).toEqual(['2026-01-31', '2026-01-31']);
});

test('MESP-135 close and report workspaces preserve Arabic RTL presentation', async ({ page }) => {
  await setupMesp135Routes(page);
  await page.goto('/app/finance/close');
  await page.locator('.language-button').click();
  await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
  await expect(page.locator('.close-page h1')).not.toHaveText('Close periods with evidence');
  await page.goto('/app/finance/reports');
  await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
  await expect(page.locator('.reports-page h1')).not.toHaveText('Core reports from posted facts');
});

test('Tax/FX workspace renders meaningful Arabic labels and RTL direction', async ({ page }) => {
  await setupTaxFxRoutes(page);
  await page.goto('/app/finance/tax-fx');
  const workspace = page.locator('[data-testid="finance-tax-fx-workspace"]');
  await page.locator('.language-button').click();
  await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
  await expect(workspace.getByRole('heading', { level: 1 })).toContainText('الضرائب');
  await workspace.getByRole('tab', { name: 'إعادة التقييم' }).click();
  await expect(workspace).toContainText('تسوية فروق العملة المحققة');
});
