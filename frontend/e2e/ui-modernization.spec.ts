import { expect, test, type Locator, type Page, type Route } from '@playwright/test';

const tenantId = 'tenant-ui';
const companyId = 'company-ui';
const operationalContexts = [
  { contextId: 'operation-ui', kind: 'Company', displayName: 'Alpha Company', eligibilityVersion: 1 },
  { contextId: 'operation-branch-ui', kind: 'Branch', displayName: 'Alpha Branch', eligibilityVersion: 1 },
  { contextId: 'operation-company-ui', kind: 'Company', displayName: 'Beta Company', eligibilityVersion: 1 },
];
const orderRows = [
  { id: 'po-1', status: 'Issued', supplierCode: 'SUP-01', supplierName: 'Aster Supplies', supplierQuotationReference: 'QT-001', currencyCode: 'SAR', total: 100, lineCount: 2, createdAt: '2026-08-01T08:00:00Z', updatedAt: '2026-08-10T08:00:00Z', version: 'PO-V1' },
  { id: 'po-2', status: 'Approved', supplierCode: 'SUP-02', supplierName: 'Birch Industrial', supplierQuotationReference: 'QT-002', currencyCode: 'USD', total: 250, lineCount: 3, createdAt: '2026-08-02T08:00:00Z', updatedAt: '2026-08-11T08:00:00Z', version: 'PO-V1' },
  { id: 'po-3', status: 'Draft', supplierCode: 'SUP-03', supplierName: 'Cedar Office', supplierQuotationReference: 'QT-003', currencyCode: 'SAR', total: 400, lineCount: 1, createdAt: '2026-08-03T08:00:00Z', updatedAt: '2026-08-12T08:00:00Z', version: 'PO-V1' },
  { id: 'po-4', status: 'Issued', supplierCode: 'SUP-04', supplierName: 'Dune Trading', supplierQuotationReference: 'QT-004', currencyCode: 'EUR', total: 550, lineCount: 4, createdAt: '2026-08-04T08:00:00Z', updatedAt: '2026-08-13T08:00:00Z', version: 'PO-V1' },
  { id: 'po-5', status: 'Approved', supplierCode: 'SUP-05', supplierName: 'Elm Services', supplierQuotationReference: 'QT-005', currencyCode: 'USD', total: 700, lineCount: 2, createdAt: '2026-08-05T08:00:00Z', updatedAt: '2026-08-14T08:00:00Z', version: 'PO-V1' },
  { id: 'po-6', status: 'Draft', supplierCode: 'SUP-06', supplierName: 'Fennel Logistics', supplierQuotationReference: 'QT-006', currencyCode: 'SAR', total: 850, lineCount: 3, createdAt: '2026-08-06T08:00:00Z', updatedAt: '2026-08-15T08:00:00Z', version: 'PO-V1' },
  { id: 'po-7', status: 'Issued', supplierCode: 'SUP-07', supplierName: 'Grove Manufacturing', supplierQuotationReference: 'QT-007', currencyCode: 'EUR', total: 1000, lineCount: 5, createdAt: '2026-08-07T08:00:00Z', updatedAt: '2026-08-16T08:00:00Z', version: 'PO-V1' },
  { id: 'po-8', status: 'Approved', supplierCode: 'SUP-08', supplierName: 'Harbor Goods', supplierQuotationReference: 'QT-008', currencyCode: 'USD', total: 1150, lineCount: 2, createdAt: '2026-08-08T08:00:00Z', updatedAt: '2026-08-17T08:00:00Z', version: 'PO-V1' },
];

const orderDetail = (id: string) => {
  const row = orderRows.find((candidate) => candidate.id === id) ?? orderRows[0];
  return {
    ...row,
    tenantId,
    companyId,
    branchId: null,
    createdByActorId: 'actor-ui',
    source: {
      purchaseRequestId: 'pr-ui', purchaseRequestReference: 'PR-UI-001', purchaseRequestPurpose: 'Office supplies',
      supplierQuotationId: 'quotation-ui', supplierQuotationReference: row.supplierQuotationReference,
      supplier: { id: `supplier-${id}`, code: row.supplierCode, name: row.supplierName },
      currency: { id: 'currency-ui', code: row.currencyCode, name: row.currencyCode }, paymentTerm: null,
      sourceDecisionId: 'decision-ui', sourceDecisionRationale: 'Selected by the buyer.', selectedAt: row.createdAt,
    },
    notes: null, submittedAt: row.createdAt, approvedAt: row.createdAt, issuedAt: row.createdAt, cancelledAt: null,
    latestConfirmationId: null, latestConfirmationStatus: null, approval: null,
    lines: [{ id: `line-${id}`, sourceQuotationLineId: 'quotation-line-ui', purchaseRequestLineId: 'pr-line-ui', productSku: 'SKU-UI', productName: 'Office chair', unitOfMeasureCode: 'EA', orderedQuantity: 2, confirmedQuantity: 0, remainingQuantity: 2, unitPrice: row.total / 2, discountAmount: null, discountPercentage: null, taxCode: null, taxName: null, taxRatePercentage: null, taxAmount: null, requestedNeedByDate: '2026-09-01', deliveryDate: '2026-09-05', notes: null, version: 'LINE-V1' }],
    pendingChanges: [], canEdit: false, canSubmit: false, canApprove: false, canReject: false,
    canReturnForChange: false, canIssue: false, canCancel: row.status === 'Issued', canCaptureConfirmation: false,
    canApproveSupplierChange: false, canRejectSupplierChange: false,
  };
};

const entryResponse = (defaultTheme: string | null = null, symbolAssetUrl: string | null = null, selectedOperationalContextId = 'operation-ui') => ({
  entryMode: 'Tenant',
    );
    expect(listHeights).toEqual([44]);
    expect(await procurementInput.evaluate((element) => getComputedStyle(element).height)).toBe('44px');
    const purposeCell = page.getByRole('row', { name: /Office restocking smoke test - updated/ }).locator('td').nth(2);
    await expect(purposeCell).toContainText('Office restocking smoke test - updated');
    const purposeCellFits = await purposeCell.evaluate((element) => ({
      overflowWrap: getComputedStyle(element).overflowWrap,
      whiteSpace: getComputedStyle(element).whiteSpace,
      clientWidth: element.clientWidth,
      scrollWidth: element.scrollWidth,
    }));
    expect(purposeCellFits.overflowWrap).toBe('anywhere');
    expect(purposeCellFits.whiteSpace).toBe('normal');
    expect(purposeCellFits.scrollWidth).toBeLessThanOrEqual(purposeCellFits.clientWidth);
    const numericAlignment = await page.locator('app-data-grid').evaluate((grid) => ({
      header: getComputedStyle(grid.querySelector('th.numeric .grid-sort')!).textAlign,
      cell: getComputedStyle(grid.querySelector('td.numeric')!).textAlign,
    }));
    expect(numericAlignment.header).toBe(numericAlignment.cell);
    for (const width of [1280, 1440]) {
      await page.setViewportSize({ width, height: 900 });
      const minDataColumnWidth = await page.locator('app-data-grid colgroup col:not(.selection-column):not(.action-column)').evaluateAll((columns) =>
        Math.min(...columns.map((column) => (column as HTMLElement).getBoundingClientRect().width)),
      );
      expect(minDataColumnWidth).toBeGreaterThanOrEqual(144);
    }
    await page.setViewportSize({ width: 1440, height: 900 });
    const headerLabels = await page.locator('app-data-grid .grid-sort-label').evaluateAll((labels) => labels.map((element) => ({
      text: element.textContent?.trim() ?? '',
      title: element.getAttribute('title'),
      clipped: (element as HTMLElement).scrollWidth > (element as HTMLElement).clientWidth + 1,
    })));
    expect(headerLabels.length).toBeGreaterThan(0);
    expect(headerLabels.filter((label) => label.clipped)).toEqual([]);
    expect(headerLabels.every((label) => label.title === label.text)).toBe(true);

    await page.goto('/app/procurement/purchase-requests/new');
    await expect(page.locator('input[type="date"]').first()).toBeVisible();
    const formHeights = await page.locator('input:not([type="checkbox"]):not([type="hidden"]), select').evaluateAll((elements) =>
      [...new Set(elements.filter((element) => (element as HTMLElement).offsetParent !== null).map((element) => Math.round(element.getBoundingClientRect().height)))],
    );
    expect(formHeights).toEqual([44]);
  });
});
