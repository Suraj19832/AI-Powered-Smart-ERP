using SmartERP.Domain.Common;

namespace SmartERP.Domain.Entities;

public class Customer : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? TaxNumber { get; set; }
    public decimal CreditLimit { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<SalesOrder> SalesOrders { get; set; } = new List<SalesOrder>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}

public class Supplier : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? TaxNumber { get; set; }
    /// <summary>Typical delivery lead time; used by reorder suggestions.</summary>
    public int LeadTimeDays { get; set; } = 7;
    public bool IsActive { get; set; } = true;

    public ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
