using Microsoft.EntityFrameworkCore;
using SmartERP.Application.Common;
using SmartERP.Domain.Enums;

namespace SmartERP.Application.Analytics;

public record NamedAmount(string Name, decimal Amount, decimal Quantity = 0, int Count = 0);
public record PeriodComparison(decimal Current, decimal Previous, decimal? ChangePercent);

public record SalesSummaryDto(DateTime From, DateTime To, decimal GrossSales, decimal NetSales, decimal TaxCollected, int InvoiceCount,
    decimal AverageOrderValue, decimal CostOfGoodsSold, decimal GrossProfit, decimal GrossMarginPercent, PeriodComparison VsPreviousPeriod,
    IReadOnlyList<NamedAmount> TopProducts, IReadOnlyList<NamedAmount> TopCustomers);

public record PurchaseSummaryDto(DateTime From, DateTime To, decimal TotalPurchases, int OrderCount, int OpenOrders, decimal OpenOrderValue,
    IReadOnlyList<NamedAmount> TopSuppliers);

public record ExpenseSummaryDto(DateTime From, DateTime To, decimal Total, IReadOnlyList<NamedAmount> ByCategory);

public record LowStockItemDto(int ProductId, string Sku, string Name, string Category, decimal QuantityOnHand, decimal ReorderLevel,
    string Unit, string? PreferredSupplier);

public record ReceivableDto(int InvoiceId, string InvoiceNumber, string CustomerName, DateTime InvoiceDate, DateTime DueDate,
    decimal TotalAmount, decimal BalanceDue, int DaysOverdue);

public record AgingBucket(string Bucket, decimal Amount, int Count);

public record ReceivablesSummaryDto(decimal TotalOutstanding, decimal TotalOverdue, int OpenInvoices, IReadOnlyList<AgingBucket> Aging,
    IReadOnlyList<ReceivableDto> Invoices);

public record PayableDto(int PurchaseOrderId, string OrderNumber, string SupplierName, DateTime? ReceivedDate, decimal TotalAmount, decimal BalanceDue);

public record PayablesSummaryDto(decimal TotalOutstanding, IReadOnlyList<PayableDto> Orders);

public record InventoryValuationDto(decimal TotalValue, decimal TotalRetailValue, int ProductCount, int LowStockCount, int OutOfStockCount,
    IReadOnlyList<NamedAmount> ByCategory);

public record MonthlyPoint(string Month, decimal Sales, decimal Purchases, decimal Expenses, decimal Cogs, decimal Profit);

public record CustomerInsightDto(int Id, string Code, string Name, string? City, decimal LifetimeSales, decimal Outstanding, int Orders,
    DateTime? LastOrderDate, IReadOnlyList<NamedAmount> TopProducts);

public record ProductInsightDto(int Id, string Sku, string Name, string Category, decimal QuantityOnHand, decimal ReorderLevel,
    decimal SalePrice, decimal CostPrice, decimal QtySoldLast90Days, decimal RevenueLast90Days);

/// <summary>
/// Read-only, pre-aggregated business metrics. Used by the dashboard, reports and — crucially — as the ONLY
/// data surface exposed to the AI assistant (the LLM never gets raw SQL access).
/// </summary>
public class InsightsService(IAppDbContext db)
{
    private static readonly InvoiceStatus[] LiveInvoice = [InvoiceStatus.Unpaid, InvoiceStatus.PartiallyPaid, InvoiceStatus.Paid];

    public static (DateTime From, DateTime To) Month(DateTime anyDay)
    {
        var from = new DateTime(anyDay.Year, anyDay.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return (from, from.AddMonths(1));
    }

    public async Task<SalesSummaryDto> SalesSummaryAsync(DateTime from, DateTime to, CancellationToken ct)
    {
        var invoices = db.Invoices.AsNoTracking().Where(i => LiveInvoice.Contains(i.Status) && i.InvoiceDate >= from && i.InvoiceDate < to);
        var agg = await invoices
            .Select(i => new { i.TotalAmount, Net = i.SalesOrder.SubTotal - i.SalesOrder.Discount, Tax = i.SalesOrder.TaxAmount })
            .GroupBy(_ => 1)
            .Select(g => new { Gross = g.Sum(x => x.TotalAmount), Net = g.Sum(x => x.Net), Tax = g.Sum(x => x.Tax), Count = g.Count() })
            .FirstOrDefaultAsync(ct);

        var cogs = await CogsAsync(from, to, ct);
        var span = to - from;
        var prevGross = await db.Invoices.Where(i => LiveInvoice.Contains(i.Status) && i.InvoiceDate >= from - span && i.InvoiceDate < from)
            .SumAsync(i => (decimal?)i.TotalAmount, ct) ?? 0;

        var topProducts = await db.SalesOrderDetails.AsNoTracking()
            .Where(d => d.SalesOrder.Invoice != null && LiveInvoice.Contains(d.SalesOrder.Invoice.Status)
                        && d.SalesOrder.Invoice.InvoiceDate >= from && d.SalesOrder.Invoice.InvoiceDate < to)
            .Select(d => new { d.Product.Name, d.LineTotal, d.Quantity })
            .GroupBy(d => d.Name)
            .Select(g => new { Name = g.Key, Amount = g.Sum(d => d.LineTotal), Qty = g.Sum(d => d.Quantity), Count = g.Count() })
            .OrderByDescending(x => x.Amount).Take(5).ToListAsync(ct);

        var topCustomers = await invoices
            .Select(i => new { i.Customer.Name, i.TotalAmount })
            .GroupBy(i => i.Name)
            .Select(g => new { Name = g.Key, Amount = g.Sum(i => i.TotalAmount), Count = g.Count() })
            .OrderByDescending(x => x.Amount).Take(5).ToListAsync(ct);

        var gross = agg?.Gross ?? 0;
        var net = agg?.Net ?? 0;
        var count = agg?.Count ?? 0;
        return new SalesSummaryDto(from, to, gross, net, agg?.Tax ?? 0, count, count == 0 ? 0 : Math.Round(gross / count, 2),
            cogs, net - cogs, net == 0 ? 0 : Math.Round((net - cogs) / net * 100, 1),
            new PeriodComparison(gross, prevGross, Pct(gross, prevGross)),
            topProducts.Select(x => new NamedAmount(x.Name, x.Amount, x.Qty, x.Count)).ToList(),
            topCustomers.Select(x => new NamedAmount(x.Name, x.Amount, 0, x.Count)).ToList());
    }

    /// <summary>Cost of goods sold = stock issued for orders that are still invoiced (cancelled orders excluded).</summary>
    public async Task<decimal> CogsAsync(DateTime from, DateTime to, CancellationToken ct) =>
        await db.StockTransactions.AsNoTracking()
            .Where(t => t.Type == StockTransactionType.SaleIssue && t.TransactionDate >= from && t.TransactionDate < to
                        && db.SalesOrders.Any(o => o.OrderNumber == t.Reference && o.Status == SalesOrderStatus.Invoiced))
            .SumAsync(t => (decimal?)(-t.Quantity * t.UnitCost), ct) ?? 0;

    public async Task<PurchaseSummaryDto> PurchaseSummaryAsync(DateTime from, DateTime to, CancellationToken ct)
    {
        var received = db.PurchaseOrders.AsNoTracking().Where(p => p.Status == PurchaseOrderStatus.Received && p.ReceivedDate >= from && p.ReceivedDate < to);
        var total = await received.SumAsync(p => (decimal?)p.TotalAmount, ct) ?? 0;
        var count = await received.CountAsync(ct);
        var open = db.PurchaseOrders.Where(p => p.Status == PurchaseOrderStatus.Draft || p.Status == PurchaseOrderStatus.Ordered);
        var top = await received.Select(p => new { p.Supplier.Name, p.TotalAmount }).GroupBy(p => p.Name)
            .Select(g => new { Name = g.Key, Amount = g.Sum(p => p.TotalAmount), Count = g.Count() })
            .OrderByDescending(x => x.Amount).Take(5).ToListAsync(ct);
        return new PurchaseSummaryDto(from, to, total, count, await open.CountAsync(ct), await open.SumAsync(p => (decimal?)p.TotalAmount, ct) ?? 0,
            top.Select(x => new NamedAmount(x.Name, x.Amount, 0, x.Count)).ToList());
    }

    public async Task<ExpenseSummaryDto> ExpenseSummaryAsync(DateTime from, DateTime to, CancellationToken ct)
    {
        var rows = await db.Expenses.AsNoTracking().Where(e => e.ExpenseDate >= from && e.ExpenseDate < to)
            .GroupBy(e => e.Category).Select(g => new { g.Key, Amount = g.Sum(e => e.Amount), Count = g.Count() }).ToListAsync(ct);
        var list = rows.Select(r => new NamedAmount(r.Key.ToString(), r.Amount, 0, r.Count)).OrderByDescending(r => r.Amount).ToList();
        return new ExpenseSummaryDto(from, to, list.Sum(r => r.Amount), list);
    }

    public Task<List<LowStockItemDto>> LowStockAsync(CancellationToken ct) =>
        db.Products.AsNoTracking().Where(p => p.IsActive && p.QuantityOnHand <= p.ReorderLevel)
            .OrderBy(p => p.ReorderLevel == 0 ? 1 : p.QuantityOnHand / p.ReorderLevel)
            .Select(p => new LowStockItemDto(p.Id, p.Sku, p.Name, p.Category.Name, p.QuantityOnHand, p.ReorderLevel, p.Unit,
                p.PreferredSupplier != null ? p.PreferredSupplier.Name : null))
            .ToListAsync(ct);

    public async Task<ReceivablesSummaryDto> ReceivablesAsync(CancellationToken ct, int take = 100)
    {
        var today = DateTime.UtcNow.Date;
        var open = await db.Invoices.AsNoTracking()
            .Where(i => i.Status == InvoiceStatus.Unpaid || i.Status == InvoiceStatus.PartiallyPaid)
            .Select(i => new { i.Id, i.InvoiceNumber, Customer = i.Customer.Name, i.InvoiceDate, i.DueDate, i.TotalAmount, Balance = i.TotalAmount - i.AmountPaid })
            .ToListAsync(ct);

        var list = open.Select(i => new ReceivableDto(i.Id, i.InvoiceNumber, i.Customer, i.InvoiceDate, i.DueDate, i.TotalAmount, i.Balance,
            Math.Max(0, (int)(today - i.DueDate.Date).TotalDays))).OrderByDescending(i => i.DaysOverdue).ThenByDescending(i => i.BalanceDue).ToList();

        static string Bucket(int d) => d == 0 ? "Current" : d <= 30 ? "1-30 days" : d <= 60 ? "31-60 days" : d <= 90 ? "61-90 days" : "90+ days";
        var order = new[] { "Current", "1-30 days", "31-60 days", "61-90 days", "90+ days" };
        var aging = order.Select(b => new AgingBucket(b, list.Where(i => Bucket(i.DaysOverdue) == b).Sum(i => i.BalanceDue),
            list.Count(i => Bucket(i.DaysOverdue) == b))).ToList();

        return new ReceivablesSummaryDto(list.Sum(i => i.BalanceDue), list.Where(i => i.DaysOverdue > 0).Sum(i => i.BalanceDue),
            list.Count, aging, list.Take(take).ToList());
    }

    public async Task<PayablesSummaryDto> PayablesAsync(CancellationToken ct)
    {
        var rows = await db.PurchaseOrders.AsNoTracking()
            .Where(p => p.Status == PurchaseOrderStatus.Received && p.TotalAmount > p.AmountPaid)
            .OrderBy(p => p.ReceivedDate)
            .Select(p => new PayableDto(p.Id, p.OrderNumber, p.Supplier.Name, p.ReceivedDate, p.TotalAmount, p.TotalAmount - p.AmountPaid))
            .ToListAsync(ct);
        return new PayablesSummaryDto(rows.Sum(r => r.BalanceDue), rows);
    }

    public async Task<InventoryValuationDto> InventoryValuationAsync(CancellationToken ct)
    {
        var products = db.Products.AsNoTracking().Where(p => p.IsActive);
        var byCatRaw = await products.Select(p => new { Cat = p.Category.Name, Value = p.QuantityOnHand * p.CostPrice, p.QuantityOnHand })
            .GroupBy(p => p.Cat)
            .Select(g => new { Name = g.Key, Amount = g.Sum(p => p.Value), Qty = g.Sum(p => p.QuantityOnHand), Count = g.Count() })
            .OrderByDescending(x => x.Amount).ToListAsync(ct);
        var byCat = byCatRaw.Select(x => new NamedAmount(x.Name, Math.Round(x.Amount, 2), x.Qty, x.Count)).ToList();
        return new InventoryValuationDto(
            byCat.Sum(c => c.Amount),
            await products.SumAsync(p => (decimal?)(p.QuantityOnHand * p.SalePrice), ct) ?? 0,
            await products.CountAsync(ct),
            await products.CountAsync(p => p.QuantityOnHand <= p.ReorderLevel, ct),
            await products.CountAsync(p => p.QuantityOnHand <= 0, ct),
            byCat);
    }

    public async Task<List<MonthlyPoint>> MonthlyTrendAsync(int months, CancellationToken ct)
    {
        var (thisMonth, _) = Month(DateTime.UtcNow);
        var start = thisMonth.AddMonths(-(months - 1));

        var sales = await db.Invoices.AsNoTracking().Where(i => LiveInvoice.Contains(i.Status) && i.InvoiceDate >= start)
            .Select(i => new { i.InvoiceDate.Year, i.InvoiceDate.Month, Net = i.SalesOrder.SubTotal - i.SalesOrder.Discount })
            .GroupBy(x => new { x.Year, x.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Net = g.Sum(x => x.Net) }).ToListAsync(ct);
        var purchases = await db.PurchaseOrders.AsNoTracking().Where(p => p.Status == PurchaseOrderStatus.Received && p.ReceivedDate >= start)
            .GroupBy(p => new { p.ReceivedDate!.Value.Year, p.ReceivedDate!.Value.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(p => p.SubTotal) }).ToListAsync(ct);
        var expenses = await db.Expenses.AsNoTracking().Where(e => e.ExpenseDate >= start)
            .GroupBy(e => new { e.ExpenseDate.Year, e.ExpenseDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(e => e.Amount) }).ToListAsync(ct);
        var cogs = await db.StockTransactions.AsNoTracking()
            .Where(t => t.Type == StockTransactionType.SaleIssue && t.TransactionDate >= start
                        && db.SalesOrders.Any(o => o.OrderNumber == t.Reference && o.Status == SalesOrderStatus.Invoiced))
            .GroupBy(t => new { t.TransactionDate.Year, t.TransactionDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(t => -t.Quantity * t.UnitCost) }).ToListAsync(ct);

        var result = new List<MonthlyPoint>();
        for (var m = start; m <= thisMonth; m = m.AddMonths(1))
        {
            var s = sales.FirstOrDefault(x => x.Year == m.Year && x.Month == m.Month)?.Net ?? 0;
            var p = purchases.FirstOrDefault(x => x.Year == m.Year && x.Month == m.Month)?.Total ?? 0;
            var e = expenses.FirstOrDefault(x => x.Year == m.Year && x.Month == m.Month)?.Total ?? 0;
            var c = cogs.FirstOrDefault(x => x.Year == m.Year && x.Month == m.Month)?.Total ?? 0;
            result.Add(new MonthlyPoint(m.ToString("yyyy-MM"), s, p, e, Math.Round(c, 2), Math.Round(s - c - e, 2)));
        }
        return result;
    }

    public async Task<List<CustomerInsightDto>> FindCustomersAsync(string name, CancellationToken ct)
    {
        var customers = await db.Customers.AsNoTracking().Where(c => c.Name.Contains(name) || c.Code == name).Take(5)
            .Select(c => new { c.Id, c.Code, c.Name, c.City }).ToListAsync(ct);
        var result = new List<CustomerInsightDto>();
        foreach (var c in customers)
        {
            var inv = db.Invoices.Where(i => i.CustomerId == c.Id && LiveInvoice.Contains(i.Status));
            var topRaw = await db.SalesOrderDetails.Where(d => d.SalesOrder.CustomerId == c.Id && d.SalesOrder.Status == SalesOrderStatus.Invoiced)
                .Select(d => new { d.Product.Name, d.LineTotal, d.Quantity }).GroupBy(d => d.Name)
                .Select(g => new { Name = g.Key, Amount = g.Sum(d => d.LineTotal), Qty = g.Sum(d => d.Quantity), Count = g.Count() })
                .OrderByDescending(x => x.Amount).Take(3).ToListAsync(ct);
            var top = topRaw.Select(x => new NamedAmount(x.Name, x.Amount, x.Qty, x.Count)).ToList();
            result.Add(new CustomerInsightDto(c.Id, c.Code, c.Name, c.City,
                await inv.SumAsync(i => (decimal?)i.TotalAmount, ct) ?? 0,
                await inv.SumAsync(i => (decimal?)(i.TotalAmount - i.AmountPaid), ct) ?? 0,
                await inv.CountAsync(ct),
                await db.SalesOrders.Where(o => o.CustomerId == c.Id).MaxAsync(o => (DateTime?)o.OrderDate, ct),
                top));
        }
        return result;
    }

    public async Task<List<ProductInsightDto>> FindProductsAsync(string name, CancellationToken ct)
    {
        var since = DateTime.UtcNow.AddDays(-90);
        return await db.Products.AsNoTracking().Where(p => p.Name.Contains(name) || p.Sku.Contains(name)).Take(10)
            .Select(p => new ProductInsightDto(p.Id, p.Sku, p.Name, p.Category.Name, p.QuantityOnHand, p.ReorderLevel, p.SalePrice, p.CostPrice,
                db.SalesOrderDetails.Where(d => d.ProductId == p.Id && d.SalesOrder.Status == SalesOrderStatus.Invoiced && d.SalesOrder.OrderDate >= since).Sum(d => (decimal?)d.Quantity) ?? 0,
                db.SalesOrderDetails.Where(d => d.ProductId == p.Id && d.SalesOrder.Status == SalesOrderStatus.Invoiced && d.SalesOrder.OrderDate >= since).Sum(d => (decimal?)d.LineTotal) ?? 0))
            .ToListAsync(ct);
    }

    public static decimal? Pct(decimal current, decimal previous) =>
        previous == 0 ? null : Math.Round((current - previous) / previous * 100, 1);
}
