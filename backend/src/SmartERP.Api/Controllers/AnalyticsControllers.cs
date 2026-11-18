using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmartERP.Application.AI;
using SmartERP.Application.Analytics;
using SmartERP.Application.Common;

namespace SmartERP.Api.Controllers;

[ApiController, Route("api/dashboard"), Authorize]
public class DashboardController(DashboardService svc) : ControllerBase
{
    [HttpGet] public Task<DashboardDto> Get(CancellationToken ct) => svc.GetAsync(ct);
}

[ApiController, Route("api/reports"), Authorize(Roles = Roles.CanViewReports)]
public class ReportsController(ReportService reports, InsightsService insights) : ControllerBase
{
    private static (DateTime From, DateTime To) Range(DateTime? from, DateTime? to)
    {
        var f = DateTime.SpecifyKind((from ?? DateTime.UtcNow.AddMonths(-1)).Date, DateTimeKind.Utc);
        var t = DateTime.SpecifyKind((to ?? DateTime.UtcNow).Date.AddDays(1), DateTimeKind.Utc);
        return (f, t);
    }

    /// <param name="groupBy">day | month | customer | product | category</param>
    [HttpGet("sales")]
    public Task<ReportDto> Sales(DateTime? from, DateTime? to, string groupBy = "month", CancellationToken ct = default)
    { var (f, t) = Range(from, to); return reports.SalesAsync(f, t, groupBy, ct); }

    /// <param name="groupBy">day | month | supplier | product | category</param>
    [HttpGet("purchases")]
    public Task<ReportDto> Purchases(DateTime? from, DateTime? to, string groupBy = "month", CancellationToken ct = default)
    { var (f, t) = Range(from, to); return reports.PurchasesAsync(f, t, groupBy == "supplier" ? "customer" : groupBy, ct); }

    [HttpGet("profit-loss")]
    public Task<ProfitLossDto> ProfitLoss(DateTime? from, DateTime? to, CancellationToken ct = default)
    { var (f, t) = Range(from, to); return reports.ProfitLossAsync(f, t, ct); }

    [HttpGet("inventory")] public Task<List<InventoryReportRow>> Inventory(CancellationToken ct) => reports.InventoryAsync(ct);
    [HttpGet("receivables")] public Task<ReceivablesSummaryDto> Receivables(CancellationToken ct) => insights.ReceivablesAsync(ct, 500);
    [HttpGet("payables")] public Task<PayablesSummaryDto> Payables(CancellationToken ct) => insights.PayablesAsync(ct);
}

[ApiController, Route("api/ai"), Authorize, EnableRateLimiting("ai")]
public class AiController(AssistantService assistant, AiInsightsService insights, ForecastService forecast, InvoiceReaderService reader) : ControllerBase
{
    /// <summary>Natural-language questions over ERP data (LLM tool-calling over whitelisted read-only services).</summary>
    [HttpPost("chat")]
    public Task<ChatReply> Chat(ChatRequest req, CancellationToken ct) => assistant.ChatAsync(req, ct);

    [HttpGet("forecast")]
    public Task<ForecastDto> Forecast(int? productId, int months = 6, CancellationToken ct = default) =>
        productId.HasValue ? forecast.ProductForecastAsync(productId.Value, months, ct) : forecast.RevenueForecastAsync(months, ct);

    [HttpGet("forecast/products")]
    public Task<List<ProductForecastDto>> ProductForecasts(int take = 10, CancellationToken ct = default) =>
        forecast.TopProductForecastsAsync(Math.Clamp(take, 1, 30), ct);

    [HttpGet("forecast/commentary")]
    public Task<AiNarrativeDto> ForecastCommentary(CancellationToken ct) => insights.ForecastCommentaryAsync(ct);

    [HttpGet("reorder-suggestions")]
    public Task<List<ReorderSuggestionDto>> Reorder(CancellationToken ct) => forecast.ReorderSuggestionsAsync(ct);

    [HttpGet("reorder-suggestions/commentary")]
    public Task<AiNarrativeDto> ReorderCommentary(CancellationToken ct) => insights.ReorderCommentaryAsync(ct);

    [HttpPost("reports"), Authorize(Roles = Roles.CanViewReports)]
    public Task<AiReportDto> Report(AiReportRequest req, CancellationToken ct) => insights.GenerateReportAsync(req, ct);

    /// <summary>Upload a supplier invoice (PDF/image) and extract structured data.</summary>
    [HttpPost("invoice-reader"), Authorize(Roles = Roles.CanBuy), RequestSizeLimit(InvoiceReaderService.MaxBytes + 100_000)]
    public async Task<ExtractedInvoice> ReadInvoice(IFormFile file, CancellationToken ct)
    {
        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        return await reader.ExtractAsync(new AiAttachment(ms.ToArray(), file.ContentType, Path.GetFileName(file.FileName)), ct);
    }
}
