using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using SmartERP.Application.Common;
using SmartERP.Application.Inventory;
using SmartERP.Application.Sales;
using SmartERP.Domain.Entities;
using SmartERP.Domain.Enums;

namespace SmartERP.Application.Purchasing;

public class PurchaseOrderInput
{
    [Range(1, int.MaxValue)] public int SupplierId { get; set; }
    [Range(1, int.MaxValue)] public int WarehouseId { get; set; }
    public DateTime? OrderDate { get; set; }
    public DateTime? ExpectedDate { get; set; }
    [MaxLength(50)] public string? SupplierInvoiceNumber { get; set; }
    [MaxLength(1000)] public string? Notes { get; set; }
    [MinLength(1)] public List<OrderLineInput> Lines { get; set; } = [];
}

public class PurchaseOrderQuery : PagedQuery
{
    public PurchaseOrderStatus? Status { get; set; }
    public int? SupplierId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}

public record PurchaseOrderListDto(int Id, string OrderNumber, DateTime OrderDate, DateTime? ExpectedDate, DateTime? ReceivedDate,
    int SupplierId, string SupplierName, string Status, decimal TotalAmount, decimal AmountPaid, decimal BalanceDue);

public record PurchaseOrderDto(int Id, string OrderNumber, DateTime OrderDate, DateTime? ExpectedDate, DateTime? ReceivedDate,
    int SupplierId, string SupplierName, int WarehouseId, string WarehouseName, string Status, string? SupplierInvoiceNumber,
    decimal SubTotal, decimal TaxAmount, decimal TotalAmount, decimal AmountPaid, string? Notes, IReadOnlyList<OrderLineDto> Lines,
    IReadOnlyList<InvoicePaymentDto> Payments);

public class PurchaseOrderService(IAppDbContext db, StockService stock)
{
    public async Task<PagedResult<PurchaseOrderListDto>> ListAsync(PurchaseOrderQuery q, CancellationToken ct)
    {
        var query = db.PurchaseOrders.AsNoTracking();
        if (q.Status.HasValue) query = query.Where(o => o.Status == q.Status);
        if (q.SupplierId.HasValue) query = query.Where(o => o.SupplierId == q.SupplierId);
        if (q.From.HasValue) query = query.Where(o => o.OrderDate >= q.From);
        if (q.To.HasValue) query = query.Where(o => o.OrderDate < q.To.Value.Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(q.Search))
            query = query.Where(o => o.OrderNumber.Contains(q.Search) || o.Supplier.Name.Contains(q.Search) ||
                                     (o.SupplierInvoiceNumber != null && o.SupplierInvoiceNumber.Contains(q.Search)));
        return await query.ApplySort(q.SortBy, q.Desc, "OrderDate", true).ToPagedAsync(q, o => new PurchaseOrderListDto(
            o.Id, o.OrderNumber, o.OrderDate, o.ExpectedDate, o.ReceivedDate, o.SupplierId, o.Supplier.Name, o.Status.ToString(),
            o.TotalAmount, o.AmountPaid, o.TotalAmount - o.AmountPaid), ct);
    }

    public async Task<PurchaseOrderDto> GetAsync(int id, CancellationToken ct) =>
        await db.PurchaseOrders.AsNoTracking().Where(o => o.Id == id).Select(o => new PurchaseOrderDto(
            o.Id, o.OrderNumber, o.OrderDate, o.ExpectedDate, o.ReceivedDate, o.SupplierId, o.Supplier.Name, o.WarehouseId, o.Warehouse.Name,
            o.Status.ToString(), o.SupplierInvoiceNumber, o.SubTotal, o.TaxAmount, o.TotalAmount, o.AmountPaid, o.Notes,
            o.Details.Select(d => new OrderLineDto(d.Id, d.ProductId, d.Product.Name, d.Product.Sku, d.Quantity, d.UnitCost, d.TaxRate, d.LineTotal)).ToList(),
            o.Payments.OrderBy(p => p.PaymentDate).Select(p => new InvoicePaymentDto(p.Id, p.PaymentNumber, p.PaymentDate, p.Amount, p.Method.ToString(), p.Reference)).ToList()))
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Purchase order", id);

    public async Task<int> CreateAsync(PurchaseOrderInput input, CancellationToken ct)
    {
        await ValidateHeader(input, ct);
        var date = input.OrderDate ?? DateTime.UtcNow;
        var order = new PurchaseOrder
        {
            OrderNumber = await DocumentNumbers.NextAsync(db.PurchaseOrders.Select(o => o.OrderNumber), "PO", date, ct),
            OrderDate = date
        };
        await Apply(order, input, ct);
        db.PurchaseOrders.Add(order);
        await db.SaveChangesAsync(ct);
        return order.Id;
    }

    public async Task UpdateAsync(int id, PurchaseOrderInput input, CancellationToken ct)
    {
        var order = await db.PurchaseOrders.Include(o => o.Details).FirstOrDefaultAsync(o => o.Id == id, ct) ?? throw new NotFoundException("Purchase order", id);
        if (order.Status is not (PurchaseOrderStatus.Draft or PurchaseOrderStatus.Ordered))
            throw new BusinessRuleException("Only draft or ordered purchase orders can be edited.");
        await ValidateHeader(input, ct);
        if (input.OrderDate.HasValue) order.OrderDate = input.OrderDate.Value;
        db.PurchaseOrderDetails.RemoveRange(order.Details);
        order.Details.Clear();
        await Apply(order, input, ct);
        order.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkOrderedAsync(int id, CancellationToken ct)
    {
        var order = await db.PurchaseOrders.FindAsync([id], ct) ?? throw new NotFoundException("Purchase order", id);
        if (order.Status != PurchaseOrderStatus.Draft) throw new BusinessRuleException("Only draft orders can be sent to the supplier.");
        order.Status = PurchaseOrderStatus.Ordered;
        order.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Receives goods: posts stock in and refreshes product cost prices, inside one DB transaction.</summary>
    public async Task ReceiveAsync(int id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var order = await db.PurchaseOrders.Include(o => o.Details).ThenInclude(d => d.Product)
            .FirstOrDefaultAsync(o => o.Id == id, ct) ?? throw new NotFoundException("Purchase order", id);
        if (order.Status is not (PurchaseOrderStatus.Draft or PurchaseOrderStatus.Ordered))
            throw new BusinessRuleException($"Purchase order is already {order.Status}.");

        var now = DateTime.UtcNow;
        foreach (var d in order.Details)
        {
            // weighted average cost
            var p = d.Product;
            var newQty = p.QuantityOnHand + d.Quantity;
            if (newQty > 0 && p.QuantityOnHand >= 0)
                p.CostPrice = Math.Round((p.QuantityOnHand * p.CostPrice + d.Quantity * d.UnitCost) / newQty, 4);
            stock.Post(p, order.WarehouseId, StockTransactionType.PurchaseReceipt, d.Quantity, d.UnitCost, order.OrderNumber, null, now);
        }
        order.Status = PurchaseOrderStatus.Received;
        order.ReceivedDate = now;
        order.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task CancelAsync(int id, CancellationToken ct)
    {
        var order = await db.PurchaseOrders.FindAsync([id], ct) ?? throw new NotFoundException("Purchase order", id);
        if (order.Status is not (PurchaseOrderStatus.Draft or PurchaseOrderStatus.Ordered))
            throw new BusinessRuleException("Only draft or ordered purchase orders can be cancelled.");
        if (order.AmountPaid > 0) throw new BusinessRuleException("Purchase order has payments and cannot be cancelled.");
        order.Status = PurchaseOrderStatus.Cancelled;
        order.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var order = await db.PurchaseOrders.FindAsync([id], ct) ?? throw new NotFoundException("Purchase order", id);
        if (order.Status != PurchaseOrderStatus.Draft) throw new BusinessRuleException("Only draft purchase orders can be deleted.");
        db.PurchaseOrders.Remove(order);
        await db.SaveChangesAsync(ct);
    }

    private async Task ValidateHeader(PurchaseOrderInput input, CancellationToken ct)
    {
        if (input.Lines.Count == 0) throw new BusinessRuleException("Add at least one line.");
        if (!await db.Suppliers.AnyAsync(s => s.Id == input.SupplierId && s.IsActive, ct)) throw new BusinessRuleException("Supplier not found or inactive.");
        if (!await db.Warehouses.AnyAsync(w => w.Id == input.WarehouseId && w.IsActive, ct)) throw new BusinessRuleException("Warehouse not found or inactive.");
    }

    private async Task Apply(PurchaseOrder order, PurchaseOrderInput input, CancellationToken ct)
    {
        var ids = input.Lines.Select(l => l.ProductId).Distinct().ToList();
        if (await db.Products.CountAsync(p => ids.Contains(p.Id), ct) != ids.Count) throw new BusinessRuleException("One or more products are invalid.");

        order.SupplierId = input.SupplierId;
        order.WarehouseId = input.WarehouseId;
        order.ExpectedDate = input.ExpectedDate;
        order.SupplierInvoiceNumber = input.SupplierInvoiceNumber;
        order.Notes = input.Notes;
        foreach (var l in input.Lines)
        {
            var net = Math.Round(l.Quantity * l.UnitPrice, 2);
            order.Details.Add(new PurchaseOrderDetail
            {
                ProductId = l.ProductId, Quantity = l.Quantity, UnitCost = l.UnitPrice, TaxRate = l.TaxRate,
                LineTotal = net + Math.Round(net * l.TaxRate / 100m, 2)
            });
        }
        order.SubTotal = order.Details.Sum(d => Math.Round(d.Quantity * d.UnitCost, 2));
        order.TaxAmount = order.Details.Sum(d => d.LineTotal) - order.SubTotal;
        order.TotalAmount = order.SubTotal + order.TaxAmount;
    }
}
