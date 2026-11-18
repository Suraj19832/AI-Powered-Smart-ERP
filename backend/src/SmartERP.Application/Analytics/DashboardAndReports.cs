using Microsoft.EntityFrameworkCore;
using SmartERP.Application.Common;
using SmartERP.Domain.Enums;

namespace SmartERP.Application.Analytics;

public record KpiDto(string Key, string Label, decimal Value, decimal? ChangePercent, string Format);

public record RecentOrderDto(int Id, string OrderNumber, DateTime OrderDate, string CustomerName, decimal TotalAmount, string Status);

public record DashboardDto(IReadOnlyList<KpiDto> Kpis, IReadOnlyList<MonthlyPoint> Trend, IReadOnlyList<NamedAmount> SalesByCategory,
    IReadOnlyList<NamedAmount> TopProducts, IReadOnlyList<LowStockItemDto> LowStock, IReadOnlyList<RecentOrderDto> RecentOrders,
    IReadOnlyList<AgingBucket> ReceivablesAging);

public class DashboardService(IAppDbContext db, InsightsService insights)
{
    public async Task<DashboardDto> GetAsync(CancellationToken ct)
    {
        // month-to-date vs the same number of days last month (a like-for-like comparison)
        var now = DateTime.UtcNow;
        var (from, to) = InsightsService.Month(now);
        var pFrom = from.AddMonths(-1);
        var pTo = pFrom + (now - from) < from ? pFrom + (now - from) : from;

        var sales = await insights.SalesSummaryAsync(from, to, ct);
        var prevSales = await insights.SalesSummaryAsync(pFrom, pTo, ct);
        var purchases = await insights.PurchaseSummaryAsync(from, to, ct);
        var prevPurchases = await insights.PurchaseSummaryAsync(pFrom, pTo, ct);
        var expenses = await insights.ExpenseSummaryAsync(from, to, ct);
        var prevExpenses = await insights.ExpenseSummaryAsync(pFrom, pTo, ct);
        var receivables = await insights.ReceivablesAsync(ct, 0);
        var inventory = await insights.InventoryValuationAsync(ct);

        var profit = sales.GrossProfit - expenses.Total;
        var prevProfit = prevSales.GrossProfit - prevExpenses.Total;

        var kpis = new List<KpiDto>
        {
            new("sales", "Sales (this month)", sales.GrossSales, InsightsService.Pct(sales.GrossSales, prevSales.GrossSales), "currency"),
            new("purchases", "Purchases (this month)", purchases.TotalPurchases, InsightsService.Pct(purchases.TotalPurchases, prevPurchases.TotalPurchases), "currency"),
            new("profit", "Net profit (this month)", profit, prevProfit == 0 ? null : Math.Round((profit - prevProfit) / Math.Abs(prevProfit) * 100, 1), "currency"),
            new("stockValue", "Inventory value", inventory.TotalValue, null, "currency"),
            new("receivables", "Receivables", receivables.TotalOutstanding, null, "currency"),
            new("overdue", "Overdue receivables", receivables.TotalOverdue, null, "currency"),
            new("lowStock", "Low-stock products", inventory.LowStockCount, null, "number"),
            new("orders", "Invoices (this month)", sales.InvoiceCount, InsightsService.Pct(sales.InvoiceCount, prevSales.InvoiceCount), "number"),
        };

        var yearStart = new DateTime(DateTime.UtcNow.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var byCategory = await db.SalesOrderDetails.AsNoTracking()
            .Where(d => d.SalesOrder.Status == SalesOrderStatus.Invoiced && d.SalesOrder.OrderDate >= yearStart)
            .Select(d => new { d.Product.Category.Name, d.LineTotal, d.Quantity })
            .GroupBy(d => d.Name)
            .Select(g => new { Name = g.Key, Amount = g.Sum(d => d.LineTotal), Qty = g.Sum(d => d.Quantity) })
            .OrderByDescending(x => x.Amount).ToListAsync(ct);

        var topProducts = await db.SalesOrderDetails.AsNoTracking()
            .Where(d => d.SalesOrder.Status == SalesOrderStatus.Invoiced && d.SalesOrder.OrderDate >= from.AddMonths(-2))
            .Select(d => new { d.Product.Name, d.LineTotal, d.Quantity })
            .GroupBy(d => d.Name)
            .Select(g => new { Name = g.Key, Amount = g.Sum(d => d.LineTotal), Qty = g.Sum(d => d.Quantity) })
            .OrderByDescending(x => x.Amount).Take(6).ToListAsync(ct);

        var recent = await db.SalesOrders.AsNoTracking().OrderByDescending(o => o.OrderDate).Take(8)
            .Select(o => new RecentOrderDto(o.Id, o.OrderNumber, o.OrderDate, o.Customer.Name, o.TotalAmount, o.Status.ToString())).ToListAsync(ct);

        return new DashboardDto(kpis, await insights.MonthlyTrendAsync(12, ct),
            byCategory.Select(x => new NamedAmount(x.Name, x.Amount, x.Qty)).ToList(),
            topProducts.Select(x => new NamedAmount(x.Name, x.Amount, x.Qty)).ToList(),
            (await insights.LowStockAsync(ct)).Take(8).ToList(), recent, receivables.Aging);
    }
}

public record ReportRow(string Key, string Label, int Documents, decimal Quantity, decimal Amount);
public record ReportDto(string Title, DateTime From, DateTime To, string GroupBy, decimal TotalAmount, IReadOnlyList<ReportRow> Rows);

public record ProfitLossDto(DateTime From, DateTime To, decimal NetSales, decimal CostOfGoodsSold, decimal GrossProfit,
    IReadOnlyList<NamedAmount> Expenses, decimal TotalExpenses, decimal NetProfit, decimal NetMarginPercent);

public record InventoryReportRow(int ProductId, string Sku, string Name, string Category, string Unit, decimal QuantityOnHand,
    decimal ReorderLevel, decimal CostPrice, decimal SalePrice, decimal StockValue, decimal RetailValue, bool IsLowStock);

public class ReportService(IAppDbContext db, InsightsService insights)
{
    /// <summary>groupBy: day | month | customer | product | category</summary>
    public async Task<ReportDto> SalesAsync(DateTime from, DateTime to, string groupBy, CancellationToken ct)
    {
        var lines = await db.SalesOrderDetails.AsNoTracking()
            .Where(d => d.SalesOrder.Status == SalesOrderStatus.Invoiced && d.SalesOrder.Invoice != null
                        && d.SalesOrder.Invoice.InvoiceDate >= from && d.SalesOrder.Invoice.InvoiceDate < to)
            .Select(d => new Line(d.SalesOrderId, d.SalesOrder.Invoice!.InvoiceDate, d.SalesOrder.CustomerId, d.SalesOrder.Customer.Name,
                d.ProductId, d.Product.Name, d.Product.Category.Name, d.Quantity, d.LineTotal))
            .ToListAsync(ct);
        return Build("Sales report", from, to, groupBy, lines);
    }

    public async Task<ReportDto> PurchasesAsync(DateTime from, DateTime to, string groupBy, CancellationToken ct)
    {
        var lines = await db.PurchaseOrderDetails.AsNoTracking()
            .Where(d => d.PurchaseOrder.Status == PurchaseOrderStatus.Received && d.PurchaseOrder.ReceivedDate >= from && d.PurchaseOrder.ReceivedDate < to)
            .Select(d => new Line(d.PurchaseOrderId, d.PurchaseOrder.ReceivedDate!.Value, d.PurchaseOrder.SupplierId, d.PurchaseOrder.Supplier.Name,
                d.ProductId, d.Product.Name, d.Product.Category.Name, d.Quantity, d.LineTotal))
            .ToListAsync(ct);
        return Build("Purchase report", from, to, groupBy is "customer" ? "supplier" : groupBy, lines);
    }

    public async Task<ProfitLossDto> ProfitLossAsync(DateTime from, DateTime to, CancellationToken ct)
    {
        var sales = await insights.SalesSummaryAsync(from, to, ct);
        var expenses = await insights.ExpenseSummaryAsync(from, to, ct);
        var net = sales.GrossProfit - expenses.Total;
        return new ProfitLossDto(from, to, sales.NetSales, sales.CostOfGoodsSold, sales.GrossProfit, expenses.ByCategory, expenses.Total,
            net, sales.NetSales == 0 ? 0 : Math.Round(net / sales.NetSales * 100, 1));
    }

    public Task<List<InventoryReportRow>> InventoryAsync(CancellationToken ct) =>
        db.Products.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.Category.Name).ThenBy(p => p.Name)
            .Select(p => new InventoryReportRow(p.Id, p.Sku, p.Name, p.Category.Name, p.Unit, p.QuantityOnHand, p.ReorderLevel, p.CostPrice,
                p.SalePrice, p.QuantityOnHand * p.CostPrice, p.QuantityOnHand * p.SalePrice, p.QuantityOnHand <= p.ReorderLevel))
            .ToListAsync(ct);

    private record Line(int DocId, DateTime Date, int PartyId, string PartyName, int ProductId, string ProductName, string Category, decimal Qty, decimal Amount);

    private static ReportDto Build(string title, DateTime from, DateTime to, string groupBy, List<Line> lines)
    {
        Func<Line, (string Key, string Label)> key = groupBy switch
        {
            "day" => l => (l.Date.ToString("yyyy-MM-dd"), l.Date.ToString("dd MMM yyyy")),
            "customer" or "supplier" => l => (l.PartyId.ToString(), l.PartyName),
            "product" => l => (l.ProductId.ToString(), l.ProductName),
            "category" => l => (l.Category, l.Category),
            _ => l => (l.Date.ToString("yyyy-MM"), l.Date.ToString("MMM yyyy")),
        };
        var rows = lines.GroupBy(key)
            .Select(g => new ReportRow(g.Key.Key, g.Key.Label, g.Select(l => l.DocId).Distinct().Count(), g.Sum(l => l.Qty), g.Sum(l => l.Amount)))
            .ToList();
        rows = groupBy is "day" or "month" ? rows.OrderBy(r => r.Key).ToList() : rows.OrderByDescending(r => r.Amount).ToList();
        return new ReportDto(title, from, to, groupBy, rows.Sum(r => r.Amount), rows);
    }
}
