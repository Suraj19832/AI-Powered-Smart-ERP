using SmartERP.Domain.Common;
using SmartERP.Domain.Enums;

namespace SmartERP.Domain.Entities;

public class Category : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();
}

public class Warehouse : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Location { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Product : BaseEntity
{
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Unit { get; set; } = "pcs";
    public decimal CostPrice { get; set; }
    public decimal SalePrice { get; set; }
    public decimal TaxRate { get; set; }
    /// <summary>Denormalised on-hand quantity, kept in sync with StockTransactions inside DB transactions.</summary>
    public decimal QuantityOnHand { get; set; }
    public decimal ReorderLevel { get; set; }
    public bool IsActive { get; set; } = true;

    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public int? PreferredSupplierId { get; set; }
    public Supplier? PreferredSupplier { get; set; }

    /// <summary>Optimistic concurrency token so two simultaneous stock movements can't overwrite each other.</summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public class StockTransaction : BaseEntity
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;
    public StockTransactionType Type { get; set; }
    /// <summary>Signed quantity: positive = stock in, negative = stock out.</summary>
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
    public string? Reference { get; set; }
    public string? Notes { get; set; }
}
