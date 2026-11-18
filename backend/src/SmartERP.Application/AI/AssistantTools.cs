using System.Text.Json;
using System.Text.Json.Nodes;
using SmartERP.Application.Analytics;

namespace SmartERP.Application.AI;

/// <summary>
/// Whitelisted, read-only functions the LLM may call. Each tool maps to a vetted .NET service method;
/// the model can only choose a tool and supply validated parameters — it never sees or writes SQL.
/// </summary>
public class AssistantTools(InsightsService insights, ForecastService forecast, ReportService reports)
{
    public record Tool(AiToolDefinition Definition, Func<JsonElement, CancellationToken, Task<object>> Handler);

    private static JsonObject PeriodSchema(params (string Name, JsonObject Schema)[] extra)
    {
        var props = new JsonObject
        {
            ["period"] = new JsonObject
            {
                ["type"] = "string",
                ["enum"] = new JsonArray("today", "yesterday", "this_week", "last_week", "this_month", "last_month", "this_quarter", "last_quarter", "this_year", "last_year", "last_30_days", "last_90_days"),
                ["description"] = "Relative period. Ignored if from/to are given."
            },
            ["from"] = new JsonObject { ["type"] = "string", ["description"] = "Start date inclusive, yyyy-MM-dd" },
            ["to"] = new JsonObject { ["type"] = "string", ["description"] = "End date inclusive, yyyy-MM-dd" },
        };
        foreach (var (n, s) in extra) props[n] = s;
        return new JsonObject { ["type"] = "object", ["properties"] = props };
    }

    private static JsonObject Obj(params (string Name, JsonObject Schema, bool Required)[] props)
    {
        var p = new JsonObject();
        var req = new JsonArray();
        foreach (var (n, s, r) in props) { p[n] = s; if (r) req.Add(n); }
        return new JsonObject { ["type"] = "object", ["properties"] = p, ["required"] = req };
    }

    public IReadOnlyList<Tool> All => _all ??= Build();
    private IReadOnlyList<Tool>? _all;

    public Tool? Find(string name) => All.FirstOrDefault(t => t.Definition.Name == name);

    private List<Tool> Build() =>
    [
        new(new("get_sales_summary", "Sales totals for a period: gross/net sales, invoice count, average order, COGS, gross profit & margin, change vs previous period, top 5 products and customers.", PeriodSchema()),
            async (a, ct) => { var (f, t) = Period(a); return await insights.SalesSummaryAsync(f, t, ct); }),

        new(new("get_purchase_summary", "Purchases received in a period, open purchase orders and top suppliers.", PeriodSchema()),
            async (a, ct) => { var (f, t) = Period(a); return await insights.PurchaseSummaryAsync(f, t, ct); }),

        new(new("get_expense_summary", "Operating expenses in a period, broken down by category.", PeriodSchema()),
            async (a, ct) => { var (f, t) = Period(a); return await insights.ExpenseSummaryAsync(f, t, ct); }),

        new(new("get_profit_and_loss", "Profit & loss statement for a period: net sales, COGS, gross profit, expenses, net profit.", PeriodSchema()),
            async (a, ct) => { var (f, t) = Period(a); return await reports.ProfitLossAsync(f, t, ct); }),

        new(new("get_low_stock_products", "Active products whose on-hand quantity is at or below their reorder level.", Obj()),
            async (_, ct) => await insights.LowStockAsync(ct)),

        new(new("get_reorder_suggestions", "AI/statistical reorder recommendations: suggested quantity, urgency, days of cover and reason, based on 90-day demand, supplier lead time and safety stock.", Obj()),
            async (_, ct) => await forecast.ReorderSuggestionsAsync(ct)),

        new(new("get_pending_customer_payments", "Outstanding customer invoices (accounts receivable) with aging buckets and days overdue.",
                Obj(("overdue_only", new JsonObject { ["type"] = "boolean" }, false))),
            async (a, ct) =>
            {
                var r = await insights.ReceivablesAsync(ct, 50);
                return Bool(a, "overdue_only") ? r with { Invoices = r.Invoices.Where(i => i.DaysOverdue > 0).ToList() } : r;
            }),

        new(new("get_supplier_payables", "Unpaid balances owed to suppliers for received purchase orders.", Obj()),
            async (_, ct) => await insights.PayablesAsync(ct)),

        new(new("get_inventory_valuation", "Total inventory value at cost and retail, product counts, low/out-of-stock counts, value by category.", Obj()),
            async (_, ct) => await insights.InventoryValuationAsync(ct)),

        new(new("get_monthly_trend", "Month-by-month net sales, purchases, expenses, COGS and profit.",
                Obj(("months", new JsonObject { ["type"] = "integer", ["minimum"] = 1, ["maximum"] = 24 }, false))),
            async (a, ct) => await insights.MonthlyTrendAsync(Math.Clamp(Int(a, "months") ?? 6, 1, 24), ct)),

        new(new("find_customer", "Look up customers by (partial) name or code: lifetime sales, outstanding balance, last order, top products.",
                Obj(("name", new JsonObject { ["type"] = "string" }, true))),
            async (a, ct) => await insights.FindCustomersAsync(Str(a, "name") ?? "", ct)),

        new(new("find_product", "Look up products by (partial) name or SKU: stock, prices, 90-day units sold and revenue.",
                Obj(("name", new JsonObject { ["type"] = "string" }, true))),
            async (a, ct) => await insights.FindProductsAsync(Str(a, "name") ?? "", ct)),

        new(new("get_sales_forecast", "Forecast future monthly net sales (Holt linear trend). Optionally for one product id.",
                Obj(("months_ahead", new JsonObject { ["type"] = "integer", ["minimum"] = 1, ["maximum"] = 12 }, false),
                    ("product_id", new JsonObject { ["type"] = "integer" }, false))),
            async (a, ct) =>
            {
                var h = Math.Clamp(Int(a, "months_ahead") ?? 3, 1, 12);
                var pid = Int(a, "product_id");
                var f = pid.HasValue ? await forecast.ProductForecastAsync(pid.Value, h, ct) : await forecast.RevenueForecastAsync(h, ct);
                // keep the payload small for the model: last 6 actuals + forecast
                return f with { Points = f.Points.Where(p => p.Actual == null).Concat(f.Points.Where(p => p.Actual != null).TakeLast(6)).OrderBy(p => p.Month).ToList() };
            }),
    ];

    // ---------- argument helpers ----------

    public static (DateTime From, DateTime To) Period(JsonElement a)
    {
        var from = Date(a, "from");
        var to = Date(a, "to");
        if (from.HasValue || to.HasValue)
            return (from ?? to!.Value.AddMonths(-1), (to ?? DateTime.UtcNow.Date).AddDays(1));
        return Resolve(Str(a, "period") ?? "this_month");
    }

    public static (DateTime From, DateTime To) Resolve(string period)
    {
        var today = DateTime.UtcNow.Date;
        var month = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var qStart = new DateTime(today.Year, (today.Month - 1) / 3 * 3 + 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var year = new DateTime(today.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var week = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        return period switch
        {
            "today" => (today, today.AddDays(1)),
            "yesterday" => (today.AddDays(-1), today),
            "this_week" => (week, week.AddDays(7)),
            "last_week" => (week.AddDays(-7), week),
            "last_month" => (month.AddMonths(-1), month),
            "this_quarter" => (qStart, qStart.AddMonths(3)),
            "last_quarter" => (qStart.AddMonths(-3), qStart),
            "this_year" => (year, year.AddYears(1)),
            "last_year" => (year.AddYears(-1), year),
            "last_30_days" => (today.AddDays(-29), today.AddDays(1)),
            "last_90_days" => (today.AddDays(-89), today.AddDays(1)),
            _ => (month, month.AddMonths(1)),
        };
    }

    private static string? Str(JsonElement a, string n) =>
        a.ValueKind == JsonValueKind.Object && a.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int? Int(JsonElement a, string n) =>
        a.ValueKind == JsonValueKind.Object && a.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : null;

    private static bool Bool(JsonElement a, string n) =>
        a.ValueKind == JsonValueKind.Object && a.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.True;

    private static DateTime? Date(JsonElement a, string n) =>
        DateTime.TryParse(Str(a, n), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var d)
            ? DateTime.SpecifyKind(d.Date, DateTimeKind.Utc) : null;
}
