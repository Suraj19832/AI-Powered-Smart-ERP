using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using SmartERP.Application.Common;
using SmartERP.Application.Inventory;
using SmartERP.Domain.Entities;
using SmartERP.Domain.Enums;

namespace SmartERP.Application.Sales;

public class OrderLineInput
{
    [Range(1, int.MaxValue)] public int ProductId { get; set; }
    [Range(0.0001, 1_000_000)] public decimal Quantity { get; set; }
    /// <summary>Unit price for sales lines / unit cost for purchase lines.</summary>
    [Range(0, 100_000_000)] public decimal UnitPrice { get; set; }
    [Range(0, 100)] public decimal TaxRate { get; set; }
}

public class SalesOrderInput
{
    [Range(1, int.MaxValue)] public int CustomerId { get; set; }
    [Range(1, int.MaxValue)] public int WarehouseId { get; set; }
    public DateTime? OrderDate { get; set; }
    [Range(0, 100_000_000)] public decimal Discount { get; set; }
    [MaxLength(1000)] public string? Notes { get; set; }
    [MinLength(1)] public List<OrderLineInput> Lines { get; set; } = [];
}

public class SalesOrderQuery : PagedQuery
{
    public SalesOrderStatus? Status { get; set; }
    public int? CustomerId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}

public record OrderLineDto(int Id, int ProductId, string ProductName, string Sku, decimal Quantity, decimal UnitPrice, decimal TaxRate, decimal LineTotal);

public record SalesOrderListDto(int Id, string OrderNumber, DateTime OrderDate, int CustomerId, string CustomerName, string Status,
    decimal TotalAmount, string? InvoiceNumber, int? InvoiceId);

public record SalesOrderDto(int Id, string OrderNumber, DateTime OrderDate, int CustomerId, string CustomerName, int WarehouseId,
    string WarehouseName, string Status, decimal SubTotal, decimal TaxAmount, decimal Discount, decimal TotalAmount, string? Notes,
    int? InvoiceId, string? InvoiceNumber, IReadOnlyList<OrderLineDto> Lines);

public class SalesOrderService(IAppDbContext db, StockService stock)
{
    public const int PaymentTermsDays = 30;

    public async Task<PagedResult<SalesOrderListDto>> ListAsync(SalesOrderQuery q, CancellationToken ct)
    {
        var query = db.SalesOrders.AsNoTracking();
        if (q.Status.HasValue) query = query.Where(o => o.Status == q.Status);
        if (q.CustomerId.HasValue) query = query.Where(o => o.CustomerId == q.CustomerId);
        if (q.From.HasValue) query = query.Where(o => o.OrderDate >= q.From);
        if (q.To.HasValue) query = query.Where(o => o.OrderDate < q.To.Value.Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(q.Search))
            query = query.Where(o => o.OrderNumber.Contains(q.Search) || o.Customer.Name.Contains(q.Search));
        return await query.ApplySort(q.SortBy, q.Desc, "OrderDate", true).ToPagedAsync(q, o => new SalesOrderListDto(
            o.Id, o.OrderNumber, o.OrderDate, o.CustomerId, o.Customer.Name, o.Status.ToString(), o.TotalAmount,
            o.Invoice != null ? o.Invoice.InvoiceNumber : null, o.Invoice != null ? o.Invoice.Id : null), ct);
    }

    public async Task<SalesOrderDto> GetAsync(int id, CancellationToken ct) =>
        await db.SalesOrders.AsNoTracking().Where(o => o.Id == id).Select(o => new SalesOrderDto(
            o.Id, o.OrderNumber, o.OrderDate, o.CustomerId, o.Customer.Name, o.WarehouseId, o.Warehouse.Name, o.Status.ToString(),
            o.SubTotal, o.TaxAmount, o.Discount, o.TotalAmount, o.Notes,
            o.Invoice != null ? o.Invoice.Id : null, o.Invoice != null ? o.Invoice.InvoiceNumber : null,
            o.Details.Select(d => new OrderLineDto(d.Id, d.ProductId, d.Product.Name, d.Product.Sku, d.Quantity, d.UnitPrice, d.TaxRate, d.LineTotal)).ToList()))
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Sales order", id);

    public async Task<int> CreateAsync(SalesOrderInput input, CancellationToken ct)
    {
        await ValidateHeader(input, ct);
        var date = input.OrderDate ?? DateTime.UtcNow;
        var order = new SalesOrder
        {
            OrderNumber = await DocumentNumbers.NextAsync(db.SalesOrders.Select(o => o.OrderNumber), "SO", date, ct),
            OrderDate = date
        };
        await Apply(order, input, ct);
        db.SalesOrders.Add(order);
        await db.SaveChangesAsync(ct);
        return order.Id;
    }

    public async Task UpdateAsync(int id, SalesOrderInput input, CancellationToken ct)
    {
        var order = await db.SalesOrders.Include(o => o.Details).FirstOrDefaultAsync(o => o.Id == id, ct) ?? throw new NotFoundException("Sales order", id);
        if (order.Status != SalesOrderStatus.Quotation) throw new BusinessRuleException("Only quotations can be edited.");
        await ValidateHeader(input, ct);
        if (input.OrderDate.HasValue) order.OrderDate = input.OrderDate.Value;
        db.SalesOrderDetails.RemoveRange(order.Details);
        order.Details.Clear();
        await Apply(order, input, ct);
        order.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Confirms a quotation: checks and issues stock, then raises the invoice — all in one DB transaction.</summary>
    public async Task<int> ConfirmAsync(int id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var order = await db.SalesOrders.Include(o => o.Details).ThenInclude(d => d.Product)
            .FirstOrDefaultAsync(o => o.Id == id, ct) ?? throw new NotFoundException("Sales order", id);
        if (order.Status != SalesOrderStatus.Quotation) throw new BusinessRuleException($"Order is already {order.Status}.");

        var shortages = order.Details.GroupBy(d => d.Product)
            .Where(g => g.Key.QuantityOnHand < g.Sum(d => d.Quantity))
            .Select(g => $"{g.Key.Name} (need {g.Sum(d => d.Quantity)}, have {g.Key.QuantityOnHand})").ToList();
        if (shortages.Count > 0) throw new BusinessRuleException("Insufficient stock: " + string.Join("; ", shortages));

        var now = DateTime.UtcNow;
        foreach (var d in order.Details)
            stock.Post(d.Product, order.WarehouseId, StockTransactionType.SaleIssue, -d.Quantity, d.Product.CostPrice, order.OrderNumber, null, now);

        var invoice = new Invoice
        {
            InvoiceNumber = await DocumentNumbers.NextAsync(db.Invoices.Select(i => i.InvoiceNumber), "INV", now, ct),
            SalesOrder = order, CustomerId = order.CustomerId, InvoiceDate = now, DueDate = now.AddDays(PaymentTermsDays),
            TotalAmount = order.TotalAmount, Status = InvoiceStatus.Unpaid
        };
        db.Invoices.Add(invoice);
        order.Status = SalesOrderStatus.Invoiced;
        order.UpdatedAt = now;

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return invoice.Id;
    }

    /// <summary>Cancels a quotation, or an unpaid invoiced order (stock is returned and the invoice cancelled).</summary>
    public async Task CancelAsync(int id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var order = await db.SalesOrders.Include(o => o.Details).ThenInclude(d => d.Product).Include(o => o.Invoice)
            .FirstOrDefaultAsync(o => o.Id == id, ct) ?? throw new NotFoundException("Sales order", id);
        if (order.Status == SalesOrderStatus.Cancelled) throw new BusinessRuleException("Order is already cancelled.");

        if (order.Status == SalesOrderStatus.Invoiced)
        {
            if (order.Invoice is { AmountPaid: > 0 }) throw new BusinessRuleException("Invoice has payments; it cannot be cancelled.");
            var now = DateTime.UtcNow;
            foreach (var d in order.Details)
                stock.Post(d.Product, order.WarehouseId, StockTransactionType.AdjustmentIn, d.Quantity, d.Product.CostPrice,
                    order.OrderNumber, "Sales order cancelled", now);
            if (order.Invoice != null) order.Invoice.Status = InvoiceStatus.Cancelled;
        }
        order.Status = SalesOrderStatus.Cancelled;
        order.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var order = await db.SalesOrders.FindAsync([id], ct) ?? throw new NotFoundException("Sales order", id);
        if (order.Status == SalesOrderStatus.Invoiced || await db.Invoices.AnyAsync(i => i.SalesOrderId == id, ct))
            throw new BusinessRuleException("Orders that were invoiced cannot be deleted; they are kept for audit.");
        db.SalesOrders.Remove(order);
        await db.SaveChangesAsync(ct);
    }

    private async Task ValidateHeader(SalesOrderInput input, CancellationToken ct)
    {
        if (input.Lines.Count == 0) throw new BusinessRuleException("Add at least one line.");
        if (!await db.Customers.AnyAsync(c => c.Id == input.CustomerId && c.IsActive, ct)) throw new BusinessRuleException("Customer not found or inactive.");
        if (!await db.Warehouses.AnyAsync(w => w.Id == input.WarehouseId && w.IsActive, ct)) throw new BusinessRuleException("Warehouse not found or inactive.");
    }

    private async Task Apply(SalesOrder order, SalesOrderInput input, CancellationToken ct)
    {
        var ids = input.Lines.Select(l => l.ProductId).Distinct().ToList();
        var products = await db.Products.Where(p => ids.Contains(p.Id) && p.IsActive).ToDictionaryAsync(p => p.Id, ct);
        if (products.Count != ids.Count) throw new BusinessRuleException("One or more products are invalid or inactive.");

        order.CustomerId = input.CustomerId;
        order.WarehouseId = input.WarehouseId;
        order.Notes = input.Notes;
        order.Discount = input.Discount;
        foreach (var l in input.Lines)
        {
            var net = Math.Round(l.Quantity * l.UnitPrice, 2);
            order.Details.Add(new SalesOrderDetail
            {
                ProductId = l.ProductId, Quantity = l.Quantity, UnitPrice = l.UnitPrice, TaxRate = l.TaxRate,
                LineTotal = net + Math.Round(net * l.TaxRate / 100m, 2)
            });
        }
        order.SubTotal = order.Details.Sum(d => Math.Round(d.Quantity * d.UnitPrice, 2));
        order.TaxAmount = order.Details.Sum(d => d.LineTotal) - order.SubTotal;
        if (input.Discount > order.SubTotal + order.TaxAmount) throw new BusinessRuleException("Discount exceeds order total.");
        order.TotalAmount = order.SubTotal + order.TaxAmount - order.Discount;
    }
}

// ---------- Invoices ----------

public class InvoiceQuery : PagedQuery
{
    public InvoiceStatus? Status { get; set; }
    public int? CustomerId { get; set; }
    public bool? OverdueOnly { get; set; }
    public bool? OpenOnly { get; set; }
}

public record InvoiceListDto(int Id, string InvoiceNumber, DateTime InvoiceDate, DateTime DueDate, int CustomerId, string CustomerName,
    int SalesOrderId, string OrderNumber, decimal TotalAmount, decimal AmountPaid, decimal BalanceDue, string Status, bool IsOverdue);

public record InvoicePaymentDto(int Id, string PaymentNumber, DateTime PaymentDate, decimal Amount, string Method, string? Reference);

public record InvoiceDto(InvoiceListDto Header, string? CustomerEmail, string? CustomerAddress, IReadOnlyList<OrderLineDto> Lines,
    decimal SubTotal, decimal TaxAmount, decimal Discount, IReadOnlyList<InvoicePaymentDto> Payments);

public class InvoiceService(IAppDbContext db)
{
    public async Task<PagedResult<InvoiceListDto>> ListAsync(InvoiceQuery q, CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var query = db.Invoices.AsNoTracking();
        if (q.Status.HasValue) query = query.Where(i => i.Status == q.Status);
        if (q.CustomerId.HasValue) query = query.Where(i => i.CustomerId == q.CustomerId);
        if (q.OpenOnly == true) query = query.Where(i => i.Status == InvoiceStatus.Unpaid || i.Status == InvoiceStatus.PartiallyPaid);
        if (q.OverdueOnly == true) query = query.Where(i => (i.Status == InvoiceStatus.Unpaid || i.Status == InvoiceStatus.PartiallyPaid) && i.DueDate < today);
        if (!string.IsNullOrWhiteSpace(q.Search))
            query = query.Where(i => i.InvoiceNumber.Contains(q.Search) || i.Customer.Name.Contains(q.Search));
        return await query.ApplySort(q.SortBy, q.Desc, "InvoiceDate", true).ToPagedAsync(q, i => new InvoiceListDto(
            i.Id, i.InvoiceNumber, i.InvoiceDate, i.DueDate, i.CustomerId, i.Customer.Name, i.SalesOrderId, i.SalesOrder.OrderNumber,
            i.TotalAmount, i.AmountPaid, i.TotalAmount - i.AmountPaid, i.Status.ToString(),
            (i.Status == InvoiceStatus.Unpaid || i.Status == InvoiceStatus.PartiallyPaid) && i.DueDate < today), ct);
    }

    public async Task<InvoiceDto> GetAsync(int id, CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        return await db.Invoices.AsNoTracking().Where(i => i.Id == id).Select(i => new InvoiceDto(
            new InvoiceListDto(i.Id, i.InvoiceNumber, i.InvoiceDate, i.DueDate, i.CustomerId, i.Customer.Name, i.SalesOrderId,
                i.SalesOrder.OrderNumber, i.TotalAmount, i.AmountPaid, i.TotalAmount - i.AmountPaid, i.Status.ToString(),
                (i.Status == InvoiceStatus.Unpaid || i.Status == InvoiceStatus.PartiallyPaid) && i.DueDate < today),
            i.Customer.Email, i.Customer.Address,
            i.SalesOrder.Details.Select(d => new OrderLineDto(d.Id, d.ProductId, d.Product.Name, d.Product.Sku, d.Quantity, d.UnitPrice, d.TaxRate, d.LineTotal)).ToList(),
            i.SalesOrder.SubTotal, i.SalesOrder.TaxAmount, i.SalesOrder.Discount,
            i.Payments.OrderBy(p => p.PaymentDate).Select(p => new InvoicePaymentDto(p.Id, p.PaymentNumber, p.PaymentDate, p.Amount, p.Method.ToString(), p.Reference)).ToList()))
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Invoice", id);
    }
}
