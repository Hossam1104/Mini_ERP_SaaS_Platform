export const NAVIGATION_GROUPS = [
  {
    id: 'master-data',
    labelEn: 'Master data',
    labelAr: 'البيانات الرئيسية',
    icon: 'database',
    items: [
      { path: '/app/master-data/categories', labelEn: 'Master Data', labelAr: 'البيانات الرئيسية', icon: 'database' },
      { path: '/app/price-lists', labelEn: 'Price Lists', labelAr: 'قوائم الأسعار', icon: 'tag' },
      { path: '/app/master-data/imports', labelEn: 'Imports', labelAr: 'استيراد البيانات', icon: 'upload' },
    ],
  },
  {
    id: 'procurement',
    labelEn: 'Procurement',
    labelAr: 'المشتريات',
    icon: 'shopping-cart',
    items: [
      { path: '/app/procurement/purchase-requests', labelEn: 'Purchase Requests', labelAr: 'طلبات الشراء', icon: 'shopping-cart' },
      { path: '/app/procurement/supplier-quotations', labelEn: 'Supplier Quotations', labelAr: 'عروض الموردين', icon: 'file-check' },
      { path: '/app/procurement/purchase-orders', labelEn: 'Purchase Orders', labelAr: 'أوامر الشراء', icon: 'receipt' },
      { path: '/app/procurement/goods-receipts', labelEn: 'Goods Receipts', labelAr: 'سندات الاستلام', icon: 'package-check' },
      { path: '/app/procurement/supplier-returns', labelEn: 'Supplier Returns', labelAr: 'مرتجعات الموردين', icon: 'rotate-ccw' },
      { path: '/app/procurement/invoice-handoffs', labelEn: 'Invoice Handoffs', labelAr: 'تسليم الفواتير', icon: 'arrow-left-right' },
      { path: '/app/procurement/invoice-matching', labelEn: 'Invoice Matching', labelAr: 'مطابقة الفواتير', icon: 'scan-search' },
    ],
  },
  {
    id: 'operations',
    labelEn: 'Operations',
    labelAr: 'العمليات',
    icon: 'boxes',
    items: [
      { path: '/app/inventory', labelEn: 'Inventory', labelAr: 'المخزون', icon: 'boxes' },
      { path: '/app/inventory/valuation', labelEn: 'Inventory Valuation', labelAr: 'تقييم المخزون', icon: 'chart-column' },
    ],
  },
  {
    id: 'finance-sales',
    labelEn: 'Finance and sales',
    labelAr: 'المالية والمبيعات',
    icon: 'chart-combined',
    items: [
      { path: '/app/finance', labelEn: 'Finance', labelAr: 'المالية', icon: 'chart-combined' },
      { path: '/app/sales/quotations', labelEn: 'Sales', labelAr: 'المبيعات', icon: 'chart-line' },
      { path: '/app/reporting', labelEn: 'Reporting', labelAr: 'التقارير', icon: 'chart-pie' },
    ],
  },
] as const;

export type NavigationGroup = (typeof NAVIGATION_GROUPS)[number];
export type NavigationItem = NavigationGroup['items'][number];
