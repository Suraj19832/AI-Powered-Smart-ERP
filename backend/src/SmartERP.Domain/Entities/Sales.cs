using SmartERP.Domain.Common;
using SmartERP.Domain.Enums;

namespace SmartERP.Domain.Entities;

public class SalesOrder : BaseEntity
{
    public string OrderNumber { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public int WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;
    public SalesOrderStatus Status { get; set; } = SalesOrderStatus.Quotation;
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Discount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }

    public ICollection<SalesOrderDetail> Details { get; set; } = new List<SalesOrderDetail>();
    public Invoice? Invoice { get; set; }
}

public class SalesOrderDetail
{
    public int Id { get; set; }
    public int SalesOrderId { get; set; }
    public SalesOrder SalesOrder { get; set; } = null!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }
    public decimal LineTotal { get; set; }
}

public class Invoice : BaseEntity
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public int SalesOrderId { get; set; }
    public SalesOrder SalesOrder { get; set; } = null!;
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;
    public DateTime DueDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Unpaid;

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
