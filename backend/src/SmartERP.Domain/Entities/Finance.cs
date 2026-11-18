using SmartERP.Domain.Common;
using SmartERP.Domain.Enums;

namespace SmartERP.Domain.Entities;

public class Payment : BaseEntity
{
    public string PaymentNumber { get; set; } = string.Empty;
    public PaymentDirection Direction { get; set; }
    public PaymentMethod Method { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public decimal Amount { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }

    /// <summary>Set for incoming customer receipts.</summary>
    public int? InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
    /// <summary>Set for outgoing supplier payments.</summary>
    public int? PurchaseOrderId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }
}

public class Expense : BaseEntity
{
    public DateTime ExpenseDate { get; set; } = DateTime.UtcNow;
    public ExpenseCategory Category { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public string? Reference { get; set; }
}
