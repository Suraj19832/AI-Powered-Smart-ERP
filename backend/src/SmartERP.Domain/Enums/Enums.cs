namespace SmartERP.Domain.Enums;

/// <summary>Quotation → (confirm: stock issued + invoice raised) → Invoiced. Cancelling an invoiced order reverses stock.</summary>
public enum SalesOrderStatus { Quotation = 0, Invoiced = 1, Cancelled = 2 }

public enum PurchaseOrderStatus { Draft = 0, Ordered = 1, Received = 2, Cancelled = 3 }

public enum InvoiceStatus { Unpaid = 0, PartiallyPaid = 1, Paid = 2, Cancelled = 3 }

public enum StockTransactionType { PurchaseReceipt = 0, SaleIssue = 1, AdjustmentIn = 2, AdjustmentOut = 3, OpeningBalance = 4 }

public enum PaymentDirection { Incoming = 0, Outgoing = 1 }

public enum PaymentMethod { Cash = 0, BankTransfer = 1, Card = 2, Cheque = 3, Upi = 4 }

public enum ExpenseCategory { Rent = 0, Salaries = 1, Utilities = 2, Marketing = 3, Transport = 4, Maintenance = 5, OfficeSupplies = 6, Other = 7 }
