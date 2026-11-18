using Microsoft.EntityFrameworkCore;
using SmartERP.Application.Common;
using SmartERP.Domain.Enums;

namespace SmartERP.Application.Analytics;

public record ForecastPoint(string Month, decimal? Actual, decimal? Forecast, decimal? Lower, decimal? Upper);

public record ForecastDto(string Target, string Metric, string Method, int HistoryMonths, decimal NextMonthForecast,
    decimal TrendPercentPerMonth, decimal MeanAbsolutePercentError, IReadOnlyList<ForecastPoint> Points);

public record ProductForecastDto(int ProductId, string Name, string Unit, decimal QuantityOnHand, decimal AvgMonthlyQty,
    decimal NextMonthQty, decimal Next3MonthsQty, decimal TrendPercentPerMonth, decimal? MonthsOfCover);

public enum UrgencyLevel { Critical, High, Medium, Low }

public record ReorderSuggestionDto(int ProductId, string Sku, string Name, string Category, string Unit, decimal QuantityOnHand,
    decimal OnOrder, decimal ReorderLevel, decimal AvgDailyDemand, int LeadTimeDays, decimal? DaysOfCover, decimal ReorderPoint,
    decimal SuggestedQuantity, decimal EstimatedCost, string Urgency, string Reason, int? SupplierId, string? SupplierName);

/// <summary>
/// Statistical demand forecasting (Holt's double exponential smoothing with a parameter grid search) and
/// reorder-point inventory optimisation. Pure .NET maths — the LLM is only used afterwards to narrate the results.
/// </summary>
public class ForecastService(IAppDbContext db)
{
    public async Task<ForecastDto> RevenueForecastAsync(int horizon, CancellationToken ct)
    {
        var (history, months) = await MonthlySeriesAsync(null, revenue: true, 24, ct);
        return Build("All products", "Net sales", history, months, horizon);
    }

    public async Task<ForecastDto> ProductForecastAsync(int productId, int horizon, CancellationToken ct)
    {
        var name = await db.Products.Where(p => p.Id == productId).Select(p => p.Name).FirstOrDefaultAsync(ct)
                   ?? throw new NotFoundException("Product", productId);
        var (history, months) = await MonthlySeriesAsync(productId, revenue: false, 24, ct);
        return Build(name, "Units sold", history, months, horizon);
    }

    public async Task<List<ProductForecastDto>> TopProductForecastsAsync(int take, CancellationToken ct)
    {
        var since = DateTime.UtcNow.AddMonths(-12);
        var top = await db.SalesOrderDetails.AsNoTracking()
            .Where(d => d.SalesOrder.Status == SalesOrderStatus.Invoiced && d.SalesOrder.OrderDate >= since)
            .GroupBy(d => d.ProductId).Select(g => new { ProductId = g.Key, Amount = g.Sum(d => d.LineTotal) })
            .OrderByDescending(x => x.Amount).Take(take).ToListAsync(ct);

        var result = new List<ProductForecastDto>();
        foreach (var t in top)
        {
            var p = await db.Products.AsNoTracking().Where(x => x.Id == t.ProductId).Select(x => new { x.Name, x.Unit, x.QuantityOnHand }).FirstAsync(ct);
            var (history, _) = await MonthlySeriesAsync(t.ProductId, false, 12, ct);
            var fit = Holt.Fit(history);
            var next = Holt.Project(fit, 3).Select(v => Math.Max(0, v)).ToArray();
            var avg = history.Length == 0 ? 0 : history.Average();
            result.Add(new ProductForecastDto(t.ProductId, p.Name, p.Unit, p.QuantityOnHand, R(avg), R(next[0]), R(next.Sum()),
                avg == 0 ? 0 : R(fit.Trend / avg * 100), next[0] <= 0 ? null : R((double)p.QuantityOnHand / next[0])));
        }
        return result;
    }

    public async Task<List<ReorderSuggestionDto>> ReorderSuggestionsAsync(CancellationToken ct)
    {
        const int lookbackDays = 90, coverDays = 30;
        var since = DateTime.UtcNow.AddDays(-lookbackDays);

        var demand = await db.StockTransactions.AsNoTracking()
            .Where(t => t.Type == StockTransactionType.SaleIssue && t.TransactionDate >= since)
            .GroupBy(t => new { t.ProductId, Day = t.TransactionDate.Date })
            .Select(g => new { g.Key.ProductId, g.Key.Day, Qty = g.Sum(t => -t.Quantity) })
            .ToListAsync(ct);
        var onOrder = await db.PurchaseOrderDetails.AsNoTracking()
            .Where(d => d.PurchaseOrder.Status == PurchaseOrderStatus.Draft || d.PurchaseOrder.Status == PurchaseOrderStatus.Ordered)
            .GroupBy(d => d.ProductId).Select(g => new { ProductId = g.Key, Qty = g.Sum(d => d.Quantity) })
            .ToDictionaryAsync(x => x.ProductId, x => x.Qty, ct);
        var products = await db.Products.AsNoTracking().Where(p => p.IsActive)
            .Select(p => new
            {
                p.Id, p.Sku, p.Name, Category = p.Category.Name, p.Unit, p.QuantityOnHand, p.ReorderLevel, p.CostPrice,
                p.PreferredSupplierId, Supplier = p.PreferredSupplier != null ? p.PreferredSupplier.Name : null,
                Lead = p.PreferredSupplier != null ? p.PreferredSupplier.LeadTimeDays : 7
            }).ToListAsync(ct);

        var list = new List<ReorderSuggestionDto>();
        foreach (var p in products)
        {
            var daily = new double[lookbackDays];
            foreach (var d in demand.Where(d => d.ProductId == p.Id))
            {
                var idx = (int)(d.Day - since.Date).TotalDays;
                if (idx >= 0 && idx < lookbackDays) daily[idx] += (double)d.Qty;
            }
            var mean = daily.Average();
            var sd = Math.Sqrt(daily.Select(x => (x - mean) * (x - mean)).Sum() / lookbackDays);
            var lead = Math.Max(1, p.Lead);
            // 95% service level safety stock: z * σ_daily * √leadTime
            var safety = 1.65 * sd * Math.Sqrt(lead);
            var reorderPoint = Math.Max((double)p.ReorderLevel, mean * lead + safety);
            var pipeline = (double)onOrder.GetValueOrDefault(p.Id);
            var position = (double)p.QuantityOnHand + pipeline;
            if (position > reorderPoint) continue;

            var target = mean * (lead + coverDays) + safety;
            var suggested = Math.Ceiling(Math.Max(target, (double)p.ReorderLevel * 2) - position);
            if (suggested <= 0) continue;

            double? cover = mean > 0 ? (double)p.QuantityOnHand / mean : null;
            var (urgency, reason) =
                p.QuantityOnHand <= 0 ? (UrgencyLevel.Critical, "Out of stock.") :
                cover.HasValue && cover < lead ? (UrgencyLevel.Critical, $"Only {cover:0.#} days of stock left, but supplier lead time is {lead} days — a stock-out is likely before delivery.") :
                (double)p.QuantityOnHand <= mean * lead + safety ? (UrgencyLevel.High, $"Stock is below the reorder point ({reorderPoint:0}) based on {mean:0.##}/day demand and {lead}-day lead time.") :
                (double)p.QuantityOnHand <= (double)p.ReorderLevel ? (UrgencyLevel.Medium, $"Stock is at or below the configured reorder level ({p.ReorderLevel:0}).") :
                (UrgencyLevel.Low, "Projected to reach the reorder point soon.");
            if (pipeline > 0) reason += $" {pipeline:0} already on order.";

            list.Add(new ReorderSuggestionDto(p.Id, p.Sku, p.Name, p.Category, p.Unit, p.QuantityOnHand, (decimal)pipeline, p.ReorderLevel,
                R(mean), lead, cover.HasValue ? R(cover.Value) : null, R(reorderPoint), (decimal)suggested,
                Math.Round((decimal)suggested * p.CostPrice, 2), urgency.ToString(), reason, p.PreferredSupplierId, p.Supplier));
        }
        return list.OrderBy(s => Enum.Parse<UrgencyLevel>(s.Urgency)).ThenBy(s => s.DaysOfCover ?? 0).ToList();
    }

    private async Task<(double[] History, List<DateTime> Months)> MonthlySeriesAsync(int? productId, bool revenue, int monthsBack, CancellationToken ct)
    {
        var (thisMonth, _) = InsightsService.Month(DateTime.UtcNow);
        // exclude the current (partial) month from the training data
        var start = thisMonth.AddMonths(-monthsBack);
        var q = db.SalesOrderDetails.AsNoTracking().Where(d => d.SalesOrder.Status == SalesOrderStatus.Invoiced
                                                               && d.SalesOrder.OrderDate >= start && d.SalesOrder.OrderDate < thisMonth);
        if (productId.HasValue) q = q.Where(d => d.ProductId == productId);
        var rows = await q.Select(d => new { d.SalesOrder.OrderDate.Year, d.SalesOrder.OrderDate.Month, Value = revenue ? d.Quantity * d.UnitPrice : d.Quantity })
            .GroupBy(x => new { x.Year, x.Month }).Select(g => new { g.Key.Year, g.Key.Month, Value = g.Sum(x => x.Value) }).ToListAsync(ct);

        var months = Enumerable.Range(0, monthsBack).Select(i => start.AddMonths(i)).ToList();
        // trim leading empty months (product didn't exist yet)
        var firstIdx = months.FindIndex(m => rows.Any(r => r.Year == m.Year && r.Month == m.Month));
        if (firstIdx > 0) months = months.Skip(firstIdx).ToList();
        var history = months.Select(m => (double)(rows.FirstOrDefault(r => r.Year == m.Year && r.Month == m.Month)?.Value ?? 0)).ToArray();
        return (history, months);
    }

    private static ForecastDto Build(string target, string metric, double[] history, List<DateTime> months, int horizon)
    {
        horizon = Math.Clamp(horizon, 1, 12);
        var points = new List<ForecastPoint>();
        if (history.Length < 3)
        {
            var flat = history.Length == 0 ? 0 : history.Average();
            for (var i = 0; i < history.Length; i++) points.Add(new ForecastPoint(months[i].ToString("yyyy-MM"), R(history[i]), null, null, null));
            var start = months.Count > 0 ? months[^1].AddMonths(1) : InsightsService.Month(DateTime.UtcNow).From;
            for (var h = 0; h < horizon; h++) points.Add(new ForecastPoint(start.AddMonths(h).ToString("yyyy-MM"), null, R(flat), R(flat * 0.7), R(flat * 1.3)));
            return new ForecastDto(target, metric, "Average (insufficient history)", history.Length, R(flat), 0, 0, points);
        }

        var fit = Holt.Fit(history);
        var forecast = Holt.Project(fit, horizon);
        var rmse = Math.Sqrt(fit.Residuals.Select(r => r * r).Average());
        var mape = history.Zip(fit.Fitted).Where(x => x.First > 0).Select(x => Math.Abs(x.First - x.Second) / x.First).DefaultIfEmpty(0).Average() * 100;

        for (var i = 0; i < history.Length; i++)
            points.Add(new ForecastPoint(months[i].ToString("yyyy-MM"), R(history[i]), i == history.Length - 1 ? R(history[i]) : null, null, null));
        for (var h = 0; h < horizon; h++)
        {
            var v = Math.Max(0, forecast[h]);
            var band = 1.28 * rmse * Math.Sqrt(h + 1); // ~80% interval widening with horizon
            points.Add(new ForecastPoint(months[^1].AddMonths(h + 1).ToString("yyyy-MM"), null, R(v), R(Math.Max(0, v - band)), R(v + band)));
        }
        var level = history.Average();
        return new ForecastDto(target, metric, $"Holt linear trend (α={fit.Alpha:0.0}, β={fit.Beta:0.0})", history.Length, R(Math.Max(0, forecast[0])),
            level == 0 ? 0 : R(fit.Trend / level * 100), R(mape), points);
    }

    private static decimal R(double v) => Math.Round((decimal)v, 2);

    internal static class Holt
    {
        public record Result(double Level, double Trend, double Alpha, double Beta, double[] Fitted, double[] Residuals);

        /// <summary>Grid-searches α/β to minimise one-step-ahead squared error.</summary>
        public static Result Fit(double[] y)
        {
            if (y.Length == 0) return new Result(0, 0, 0, 0, [], []);
            if (y.Length == 1) return new Result(y[0], 0, 0, 0, [y[0]], [0]);
            Result? best = null;
            var bestSse = double.MaxValue;
            for (var a = 0.1; a <= 0.91; a += 0.1)
            for (var b = 0.05; b <= 0.51; b += 0.05)
            {
                var r = Run(y, a, b);
                var sse = r.Residuals.Skip(1).Sum(e => e * e);
                if (sse < bestSse) { bestSse = sse; best = r; }
            }
            return best!;
        }

        private static Result Run(double[] y, double a, double b)
        {
            double level = y[0], trend = y[1] - y[0];
            var fitted = new double[y.Length];
            var res = new double[y.Length];
            fitted[0] = y[0];
            for (var t = 1; t < y.Length; t++)
            {
                fitted[t] = level + trend;
                res[t] = y[t] - fitted[t];
                var prevLevel = level;
                level = a * y[t] + (1 - a) * (level + trend);
                trend = b * (level - prevLevel) + (1 - b) * trend;
            }
            return new Result(level, trend, a, b, fitted, res);
        }

        public static double[] Project(Result r, int horizon) =>
            Enumerable.Range(1, horizon).Select(h => r.Level + h * r.Trend).ToArray();
    }
}
