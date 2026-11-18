using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SmartERP.Domain.Entities;

namespace SmartERP.Application.Common;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Customer> Customers { get; }
    DbSet<Supplier> Suppliers { get; }
    DbSet<Category> Categories { get; }
    DbSet<Warehouse> Warehouses { get; }
    DbSet<Product> Products { get; }
    DbSet<StockTransaction> StockTransactions { get; }
    DbSet<SalesOrder> SalesOrders { get; }
    DbSet<SalesOrderDetail> SalesOrderDetails { get; }
    DbSet<PurchaseOrder> PurchaseOrders { get; }
    DbSet<PurchaseOrderDetail> PurchaseOrderDetails { get; }
    DbSet<Invoice> Invoices { get; }
    DbSet<Payment> Payments { get; }
    DbSet<Expense> Expenses { get; }

    DatabaseFacade Database { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) CreateAccessToken(User user, IEnumerable<string> roles);
    string CreateRefreshToken();
    string Hash(string token);
    int RefreshTokenDays { get; }
}

public interface IPasswordService
{
    string Hash(User user, string password);
    bool Verify(User user, string password);
}

public interface IDateTime
{
    DateTime UtcNow { get; }
}
