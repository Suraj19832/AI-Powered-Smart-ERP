using Microsoft.Extensions.DependencyInjection;
using SmartERP.Application.AI;
using SmartERP.Application.Analytics;
using SmartERP.Application.Auth;
using SmartERP.Application.Finance;
using SmartERP.Application.Inventory;
using SmartERP.Application.MasterData;
using SmartERP.Application.Purchasing;
using SmartERP.Application.Sales;

namespace SmartERP.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<UserService>();
        services.AddScoped<CustomerService>();
        services.AddScoped<SupplierService>();
        services.AddScoped<CategoryService>();
        services.AddScoped<WarehouseService>();
        services.AddScoped<ProductService>();
        services.AddScoped<StockService>();
        services.AddScoped<SalesOrderService>();
        services.AddScoped<InvoiceService>();
        services.AddScoped<PurchaseOrderService>();
        services.AddScoped<PaymentService>();
        services.AddScoped<ExpenseService>();
        services.AddScoped<InsightsService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<ReportService>();
        services.AddScoped<ForecastService>();
        services.AddScoped<AssistantTools>();
        services.AddScoped<AssistantService>();
        services.AddScoped<AiInsightsService>();
        services.AddScoped<InvoiceReaderService>();
        return services;
    }
}
