// DTOs mirrored from the ASP.NET Core API (SmartERP.Application)

export interface Paged<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface UserInfo {
  id: number;
  userName: string;
  fullName: string;
  email: string;
  roles: string[];
}
export interface AuthResponse {
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  user: UserInfo;
}

export interface AppSettings {
  companyName: string;
  currency: string;
  locale: string;
  aiEnabled: boolean;
  aiModel: string | null;
}

export interface Lookup {
  id: number;
  name: string;
  extra?: string | null;
  price?: number | null;
  taxRate?: number | null;
  stock?: number | null;
}

export interface Customer {
  id: number;
  code: string;
  name: string;
  email?: string;
  phone?: string;
  address?: string;
  city?: string;
  taxNumber?: string;
  creditLimit: number;
  isActive: boolean;
  outstandingBalance: number;
  createdAt: string;
}
export interface Supplier {
  id: number;
  code: string;
  name: string;
  contactPerson?: string;
  email?: string;
  phone?: string;
  address?: string;
  city?: string;
  taxNumber?: string;
  leadTimeDays: number;
  isActive: boolean;
  payableBalance: number;
  createdAt: string;
}
export interface PartyTransaction {
  id: number;
  type: string;
  number: string;
  date: string;
  amount: number;
  status: string;
}
export interface CustomerDetail {
  customer: Customer;
  totalSales: number;
  totalPaid: number;
  orderCount: number;
  transactions: PartyTransaction[];
}
export interface SupplierDetail {
  supplier: Supplier;
  totalPurchases: number;
  totalPaid: number;
  orderCount: number;
  transactions: PartyTransaction[];
}

export interface Category {
  id: number;
  name: string;
  description?: string;
  productCount: number;
}
export interface Warehouse {
  id: number;
  code: string;
  name: string;
  location?: string;
  isActive: boolean;
}
export interface Product {
  id: number;
  sku: string;
  name: string;
  description?: string;
  unit: string;
  costPrice: number;
  salePrice: number;
  taxRate: number;
  quantityOnHand: number;
  reorderLevel: number;
  isActive: boolean;
  categoryId: number;
  categoryName: string;
  preferredSupplierId?: number | null;
  preferredSupplierName?: string | null;
  isLowStock: boolean;
  stockValue: number;
}
export interface StockTransaction {
  id: number;
  transactionDate: string;
  type: string;
  productId: number;
  productName: string;
  sku: string;
  warehouseId: number;
  warehouseName: string;
  quantity: number;
  unitCost: number;
  reference?: string;
  notes?: string;
}

export interface OrderLine {
  id: number;
  productId: number;
  productName: string;
  sku: string;
  quantity: number;
  unitPrice: number;
  taxRate: number;
  lineTotal: number;
}
export interface DocPayment {
  id: number;
  paymentNumber: string;
  paymentDate: string;
  amount: number;
  method: string;
  reference?: string;
}

export interface SalesOrderListItem {
  id: number;
  orderNumber: string;
  orderDate: string;
  customerId: number;
  customerName: string;
  status: string;
  totalAmount: number;
  invoiceNumber?: string | null;
  invoiceId?: number | null;
}
export interface SalesOrder {
  id: number;
  orderNumber: string;
  orderDate: string;
  customerId: number;
  customerName: string;
  warehouseId: number;
  warehouseName: string;
  status: string;
  subTotal: number;
  taxAmount: number;
  discount: number;
  totalAmount: number;
  notes?: string;
  invoiceId?: number | null;
  invoiceNumber?: string | null;
  lines: OrderLine[];
}
export interface InvoiceListItem {
  id: number;
  invoiceNumber: string;
  invoiceDate: string;
  dueDate: string;
  customerId: number;
  customerName: string;
  salesOrderId: number;
  orderNumber: string;
  totalAmount: number;
  amountPaid: number;
  balanceDue: number;
  status: string;
  isOverdue: boolean;
}
export interface Invoice {
  header: InvoiceListItem;
  customerEmail?: string;
  customerAddress?: string;
  lines: OrderLine[];
  subTotal: number;
  taxAmount: number;
  discount: number;
  payments: DocPayment[];
}
export interface PurchaseOrderListItem {
  id: number;
  orderNumber: string;
  orderDate: string;
  expectedDate?: string | null;
  receivedDate?: string | null;
  supplierId: number;
  supplierName: string;
  status: string;
  totalAmount: number;
  amountPaid: number;
  balanceDue: number;
}
export interface PurchaseOrder extends PurchaseOrderListItem {
  warehouseId: number;
  warehouseName: string;
  supplierInvoiceNumber?: string | null;
  subTotal: number;
  taxAmount: number;
  notes?: string;
  lines: OrderLine[];
  payments: DocPayment[];
}
export interface Payment {
  id: number;
  paymentNumber: string;
  paymentDate: string;
  direction: string;
  method: string;
  amount: number;
  reference?: string;
  notes?: string;
  invoiceId?: number | null;
  invoiceNumber?: string | null;
  purchaseOrderId?: number | null;
  purchaseOrderNumber?: string | null;
  partyName: string;
}
export interface Expense {
  id: number;
  expenseDate: string;
  category: string;
  description: string;
  amount: number;
  method: string;
  reference?: string;
}

export interface NamedAmount {
  name: string;
  amount: number;
  quantity: number;
  count: number;
}
export interface Kpi {
  key: string;
  label: string;
  value: number;
  changePercent?: number | null;
  format: "currency" | "number";
}
export interface MonthlyPoint {
  month: string;
  sales: number;
  purchases: number;
  expenses: number;
  cogs: number;
  profit: number;
}
export interface LowStockItem {
  productId: number;
  sku: string;
  name: string;
  category: string;
  quantityOnHand: number;
  reorderLevel: number;
  unit: string;
  preferredSupplier?: string | null;
}
export interface AgingBucket {
  bucket: string;
  amount: number;
  count: number;
}
export interface Dashboard {
  kpis: Kpi[];
  trend: MonthlyPoint[];
  salesByCategory: NamedAmount[];
  topProducts: NamedAmount[];
  lowStock: LowStockItem[];
  recentOrders: {
    id: number;
    orderNumber: string;
    orderDate: string;
    customerName: string;
    totalAmount: number;
    status: string;
  }[];
  receivablesAging: AgingBucket[];
}

export interface ReportRow {
  key: string;
  label: string;
  documents: number;
  quantity: number;
  amount: number;
}
export interface Report {
  title: string;
  from: string;
  to: string;
  groupBy: string;
  totalAmount: number;
  rows: ReportRow[];
}
export interface ProfitLoss {
  from: string;
  to: string;
  netSales: number;
  costOfGoodsSold: number;
  grossProfit: number;
  expenses: NamedAmount[];
  totalExpenses: number;
  netProfit: number;
  netMarginPercent: number;
}
export interface InventoryRow {
  productId: number;
  sku: string;
  name: string;
  category: string;
  unit: string;
  quantityOnHand: number;
  reorderLevel: number;
  costPrice: number;
  salePrice: number;
  stockValue: number;
  retailValue: number;
  isLowStock: boolean;
}
export interface Receivable {
  invoiceId: number;
  invoiceNumber: string;
  customerName: string;
  invoiceDate: string;
  dueDate: string;
  totalAmount: number;
  balanceDue: number;
  daysOverdue: number;
}
export interface Receivables {
  totalOutstanding: number;
  totalOverdue: number;
  openInvoices: number;
  aging: AgingBucket[];
  invoices: Receivable[];
}

export interface ForecastPoint {
  month: string;
  actual?: number | null;
  forecast?: number | null;
  lower?: number | null;
  upper?: number | null;
}
export interface Forecast {
  target: string;
  metric: string;
  method: string;
  historyMonths: number;
  nextMonthForecast: number;
  trendPercentPerMonth: number;
  meanAbsolutePercentError: number;
  points: ForecastPoint[];
}
export interface ProductForecast {
  productId: number;
  name: string;
  unit: string;
  quantityOnHand: number;
  avgMonthlyQty: number;
  nextMonthQty: number;
  next3MonthsQty: number;
  trendPercentPerMonth: number;
  monthsOfCover?: number | null;
}
export interface ReorderSuggestion {
  productId: number;
  sku: string;
  name: string;
  category: string;
  unit: string;
  quantityOnHand: number;
  onOrder: number;
  reorderLevel: number;
  avgDailyDemand: number;
  leadTimeDays: number;
  daysOfCover?: number | null;
  reorderPoint: number;
  suggestedQuantity: number;
  estimatedCost: number;
  urgency: "Critical" | "High" | "Medium" | "Low";
  reason: string;
  supplierId?: number | null;
  supplierName?: string | null;
}
export interface Narrative {
  markdown: string;
  mode: string;
}
export interface AiReport {
  title: string;
  type: string;
  period: string;
  markdown: string;
  mode: string;
  generatedAt: string;
}

export interface ChatTurn {
  role: "user" | "assistant";
  content: string;
}
export interface ChatReply {
  reply: string;
  toolsUsed: { name: string; arguments: string }[];
  mode: string;
}

export interface ExtractedLine {
  description: string;
  quantity: number;
  unitPrice: number;
  taxRate: number;
  amount: number;
  matchedProductId?: number | null;
  matchedProductName?: string | null;
  matchScore: number;
}
export interface ExtractedInvoice {
  invoiceNumber?: string | null;
  invoiceDate?: string | null;
  dueDate?: string | null;
  supplierName?: string | null;
  supplierTaxNumber?: string | null;
  supplierAddress?: string | null;
  currency?: string | null;
  subTotal?: number | null;
  taxAmount?: number | null;
  total?: number | null;
  items: ExtractedLine[];
  notes?: string | null;
  matchedSupplierId?: number | null;
  matchedSupplierName?: string | null;
  warnings: string[];
}

export interface UserRow {
  id: number;
  userName: string;
  fullName: string;
  email: string;
  isActive: boolean;
  lastLoginAt?: string | null;
  roles: string[];
}
export interface Role {
  id: number;
  name: string;
  description?: string;
}

/** Pre-fill payload passed via router state to the order form (from reorder suggestions / invoice reader). */
export interface OrderPrefill {
  partyId?: number | null;
  supplierInvoiceNumber?: string | null;
  notes?: string;
  lines: {
    productId: number;
    quantity: number;
    unitPrice: number;
    taxRate: number;
  }[];
}
