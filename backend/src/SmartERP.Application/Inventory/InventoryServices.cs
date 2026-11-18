using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using SmartERP.Application.Common;
using SmartERP.Domain.Entities;
using SmartERP.Domain.Enums;

namespace SmartERP.Application.Inventory;

// ---------- Categories ----------

public class CategoryInput
{
    [Required, MaxLength(100)] public string Name { get; set; } = "";
    [MaxLength(300)] public string? Description { get; set; }
}

public record CategoryDto(int Id, string Name, string? Description, int ProductCount);

public class CategoryService(IAppDbContext db)
{
    public Task<List<CategoryDto>> ListAsync(CancellationToken ct) =>
        db.Categories.AsNoTracking().OrderBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Description, c.Products.Count)).ToListAsync(ct);

    public async Task<int> CreateAsync(CategoryInput input, CancellationToken ct)
    {
        if (await db.Categories.AnyAsync(c => c.Name == input.Name, ct)) throw new BusinessRuleException("Category already exists.");
        var e = new Category { Name = input.Name.Trim(), Description = input.Description };
        db.Categories.Add(e);
        await db.SaveChangesAsync(ct);
        return e.Id;
    }

    public async Task UpdateAsync(int id, CategoryInput input, CancellationToken ct)
    {
        var e = await db.Categories.FindAsync([id], ct) ?? throw new NotFoundException("Category", id);
        if (await db.Categories.AnyAsync(c => c.Id != id && c.Name == input.Name, ct)) throw new BusinessRuleException("Category already exists.");
        e.Name = input.Name.Trim(); e.Description = input.Description; e.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var e = await db.Categories.FindAsync([id], ct) ?? throw new NotFoundException("Category", id);
        if (await db.Products.AnyAsync(p => p.CategoryId == id, ct)) throw new BusinessRuleException("Category has products and cannot be deleted.");
        db.Categories.Remove(e);
        await db.SaveChangesAsync(ct);
    }
}

// ---------- Warehouses ----------

public class WarehouseInput
{
    [Required, MaxLength(20)] public string Code { get; set; } = "";
    [Required, MaxLength(100)] public string Name { get; set; } = "";
    [MaxLength(200)] public string? Location { get; set; }
    public bool IsActive { get; set; } = true;
}

public record WarehouseDto(int Id, string Code, string Name, string? Location, bool IsActive);

public class WarehouseService(IAppDbContext db)
{
    public Task<List<WarehouseDto>> ListAsync(CancellationToken ct) =>
        db.Warehouses.AsNoTracking().OrderBy(w => w.Name).Select(w => new WarehouseDto(w.Id, w.Code, w.Name, w.Location, w.IsActive)).ToListAsync(ct);

    public async Task<int> CreateAsync(WarehouseInput i, CancellationToken ct)
    {
        if (await db.Warehouses.AnyAsync(w => w.Code == i.Code, ct)) throw new BusinessRuleException("Warehouse code already exists.");
        var e = new Warehouse { Code = i.Code.Trim().ToUpperInvariant(), Name = i.Name.Trim(), Location = i.Location, IsActive = i.IsActive };
        db.Warehouses.Add(e);
        await db.SaveChangesAsync(ct);
        return e.Id;
    }

    public async Task UpdateAsync(int id, WarehouseInput i, CancellationToken ct)
    {
        var e = await db.Warehouses.FindAsync([id], ct) ?? throw new NotFoundException("Warehouse", id);
        if (await db.Warehouses.AnyAsync(w => w.Id != id && w.Code == i.Code, ct)) throw new BusinessRuleException("Warehouse code already exists.");
        e.Code = i.Code.Trim().ToUpperInvariant(); e.Name = i.Name.Trim(); e.Location = i.Location; e.IsActive = i.IsActive; e.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var e = await db.Warehouses.FindAsync([id], ct) ?? throw new NotFoundException("Warehouse", id);
        if (await db.StockTransactions.AnyAsync(t => t.WarehouseId == id, ct)) throw new BusinessRuleException("Warehouse has stock history and cannot be deleted. Mark it inactive instead.");
        db.Warehouses.Remove(e);
        await db.SaveChangesAsync(ct);
    }
}

// ---------- Products ----------

public class ProductInput
{
    [Required, MaxLength(50)] public string Sku { get; set; } = "";
    [Required, MaxLength(200)] public string Name { get; set; } = "";
    [MaxLength(1000)] public string? Description { get; set; }
    [Required, MaxLength(20)] public string Unit { get; set; } = "pcs";
    [Range(0, 100_000_000)] public decimal CostPrice { get; set; }
    [Range(0, 100_000_000)] public decimal SalePrice { get; set; }
    [Range(0, 100)] public decimal TaxRate { get; set; }
    [Range(0, 100_000_000)] public decimal ReorderLevel { get; set; }
    [Range(1, int.MaxValue)] public int CategoryId { get; set; }
    public int? PreferredSupplierId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class ProductQuery : PagedQuery
{
    public int? CategoryId { get; set; }
    public bool? LowStockOnly { get; set; }
    public bool? ActiveOnly { get; set; }
}

public record ProductDto(int Id, string Sku, string Name, string? Description, string Unit, decimal CostPrice, decimal SalePrice,
    decimal TaxRate, decimal QuantityOnHand, decimal ReorderLevel, bool IsActive, int CategoryId, string CategoryName,
    int? PreferredSupplierId, string? PreferredSupplierName, bool IsLowStock, decimal StockValue);

public record LookupDto(int Id, string Name, string? Extra = null, decimal? Price = null, decimal? TaxRate = null, decimal? Stock = null);

public class ProductService(IAppDbContext db)
{
    public async Task<PagedResult<ProductDto>> ListAsync(ProductQuery q, CancellationToken ct)
    {
        var query = db.Products.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.Search))
            query = query.Where(p => p.Name.Contains(q.Search) || p.Sku.Contains(q.Search));
        if (q.CategoryId.HasValue) query = query.Where(p => p.CategoryId == q.CategoryId);
        if (q.LowStockOnly == true) query = query.Where(p => p.QuantityOnHand <= p.ReorderLevel);
        if (q.ActiveOnly == true) query = query.Where(p => p.IsActive);
        return await query.ApplySort(q.SortBy, q.Desc, "Name", false).ToPagedAsync(q, p => new ProductDto(
            p.Id, p.Sku, p.Name, p.Description, p.Unit, p.CostPrice, p.SalePrice, p.TaxRate, p.QuantityOnHand, p.ReorderLevel,
            p.IsActive, p.CategoryId, p.Category.Name, p.PreferredSupplierId, p.PreferredSupplier != null ? p.PreferredSupplier.Name : null,
            p.QuantityOnHand <= p.ReorderLevel, p.QuantityOnHand * p.CostPrice), ct);
    }

    public async Task<ProductDto> GetAsync(int id, CancellationToken ct) =>
        await db.Products.AsNoTracking().Where(p => p.Id == id)
            .Select(p => new ProductDto(p.Id, p.Sku, p.Name, p.Description, p.Unit, p.CostPrice, p.SalePrice, p.TaxRate, p.QuantityOnHand,
                p.ReorderLevel, p.IsActive, p.CategoryId, p.Category.Name, p.PreferredSupplierId,
                p.PreferredSupplier != null ? p.PreferredSupplier.Name : null, p.QuantityOnHand <= p.ReorderLevel, p.QuantityOnHand * p.CostPrice))
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Product", id);

    public Task<List<LookupDto>> LookupAsync(CancellationToken ct) =>
        db.Products.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.Name)
            .Select(p => new LookupDto(p.Id, p.Name, p.Sku, p.SalePrice, p.TaxRate, p.QuantityOnHand)).ToListAsync(ct);

    public async Task<int> CreateAsync(ProductInput i, CancellationToken ct)
    {
        await Validate(null, i, ct);
        var e = new Product();
        Map(i, e);
        db.Products.Add(e);
        await db.SaveChangesAsync(ct);
        return e.Id;
    }

    public async Task UpdateAsync(int id, ProductInput i, CancellationToken ct)
    {
        var e = await db.Products.FindAsync([id], ct) ?? throw new NotFoundException("Product", id);
        await Validate(id, i, ct);
        Map(i, e);
        e.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var e = await db.Products.FindAsync([id], ct) ?? throw new NotFoundException("Product", id);
        if (await db.StockTransactions.AnyAsync(t => t.ProductId == id, ct) ||
            await db.SalesOrderDetails.AnyAsync(d => d.ProductId == id, ct) ||
            await db.PurchaseOrderDetails.AnyAsync(d => d.ProductId == id, ct))
            throw new BusinessRuleException("Product has transactions and cannot be deleted. Mark it inactive instead.");
        db.Products.Remove(e);
        await db.SaveChangesAsync(ct);
    }

    private async Task Validate(int? id, ProductInput i, CancellationToken ct)
    {
        if (await db.Products.AnyAsync(p => p.Sku == i.Sku && p.Id != id, ct)) throw new BusinessRuleException($"SKU '{i.Sku}' is already used.");
        if (!await db.Categories.AnyAsync(c => c.Id == i.CategoryId, ct)) throw new BusinessRuleException("Category does not exist.");
        if (i.PreferredSupplierId.HasValue && !await db.Suppliers.AnyAsync(s => s.Id == i.PreferredSupplierId, ct))
            throw new BusinessRuleException("Supplier does not exist.");
    }

    private static void Map(ProductInput i, Product e)
    {
        e.Sku = i.Sku.Trim().ToUpperInvariant(); e.Name = i.Name.Trim(); e.Description = i.Description; e.Unit = i.Unit;
        e.CostPrice = i.CostPrice; e.SalePrice = i.SalePrice; e.TaxRate = i.TaxRate; e.ReorderLevel = i.ReorderLevel;
        e.CategoryId = i.CategoryId; e.PreferredSupplierId = i.PreferredSupplierId; e.IsActive = i.IsActive;
    }
}

// ---------- Stock ----------

public class StockQuery : PagedQuery
{
    public int? ProductId { get; set; }
    public int? WarehouseId { get; set; }
    public StockTransactionType? Type { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}

public record StockTransactionDto(int Id, DateTime TransactionDate, string Type, int ProductId, string ProductName, string Sku,
    int WarehouseId, string WarehouseName, decimal Quantity, decimal UnitCost, string? Reference, string? Notes);

public class StockAdjustmentRequest
{
    [Range(1, int.MaxValue)] public int ProductId { get; set; }
    [Range(1, int.MaxValue)] public int WarehouseId { get; set; }
    /// <summary>Positive adds stock, negative removes stock.</summary>
    public decimal Quantity { get; set; }
    [Required, MaxLength(300)] public string Reason { get; set; } = "";
}

public class StockService(IAppDbContext db)
{
    public async Task<PagedResult<StockTransactionDto>> ListAsync(StockQuery q, CancellationToken ct)
    {
        var query = db.StockTransactions.AsNoTracking();
        if (q.ProductId.HasValue) query = query.Where(t => t.ProductId == q.ProductId);
        if (q.WarehouseId.HasValue) query = query.Where(t => t.WarehouseId == q.WarehouseId);
        if (q.Type.HasValue) query = query.Where(t => t.Type == q.Type);
        if (q.From.HasValue) query = query.Where(t => t.TransactionDate >= q.From);
        if (q.To.HasValue) query = query.Where(t => t.TransactionDate < q.To.Value.Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(q.Search))
            query = query.Where(t => t.Product.Name.Contains(q.Search) || t.Product.Sku.Contains(q.Search) || (t.Reference != null && t.Reference.Contains(q.Search)));
        return await query.ApplySort(q.SortBy, q.Desc, "TransactionDate", true).ToPagedAsync(q, t => new StockTransactionDto(
            t.Id, t.TransactionDate, t.Type.ToString(), t.ProductId, t.Product.Name, t.Product.Sku, t.WarehouseId, t.Warehouse.Name,
            t.Quantity, t.UnitCost, t.Reference, t.Notes), ct);
    }

    public async Task AdjustAsync(StockAdjustmentRequest req, string userName, CancellationToken ct)
    {
        if (req.Quantity == 0) throw new BusinessRuleException("Quantity cannot be zero.");
        var product = await db.Products.FindAsync([req.ProductId], ct) ?? throw new NotFoundException("Product", req.ProductId);
        if (!await db.Warehouses.AnyAsync(w => w.Id == req.WarehouseId && w.IsActive, ct)) throw new BusinessRuleException("Warehouse not found or inactive.");
        if (product.QuantityOnHand + req.Quantity < 0)
            throw new BusinessRuleException($"Insufficient stock: {product.QuantityOnHand} {product.Unit} on hand.");

        Post(product, req.WarehouseId, req.Quantity > 0 ? StockTransactionType.AdjustmentIn : StockTransactionType.AdjustmentOut,
            req.Quantity, product.CostPrice, $"ADJ by {userName}", req.Reason, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Records a stock movement and updates the product's on-hand quantity. Caller saves (inside its transaction).</summary>
    public void Post(Product product, int warehouseId, StockTransactionType type, decimal signedQty, decimal unitCost,
        string? reference, string? notes, DateTime date)
    {
        product.QuantityOnHand += signedQty;
        product.UpdatedAt = DateTime.UtcNow;
        db.StockTransactions.Add(new StockTransaction
        {
            Product = product, ProductId = product.Id, WarehouseId = warehouseId, Type = type, Quantity = signedQty,
            UnitCost = unitCost, Reference = reference, Notes = notes, TransactionDate = date
        });
    }
}
