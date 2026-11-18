using Microsoft.EntityFrameworkCore;
using SmartERP.Application.Common;
using SmartERP.Domain.Entities;

namespace SmartERP.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<StockTransaction> StockTransactions => Set<StockTransaction>();
    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();
    public DbSet<SalesOrderDetail> SalesOrderDetails => Set<SalesOrderDetail>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderDetail> PurchaseOrderDetails => Set<PurchaseOrderDetail>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Expense> Expenses => Set<Expense>();

    protected override void ConfigureConventions(ModelConfigurationBuilder b)
    {
        b.Properties<decimal>().HavePrecision(18, 2);
        b.Properties<string>().HaveMaxLength(200);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        // ---------- identity ----------
        b.Entity<User>(e =>
        {
            e.HasIndex(x => x.UserName).IsUnique();
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.UserName).HasMaxLength(50);
            e.Property(x => x.PasswordHash).HasMaxLength(500);
        });
        b.Entity<Role>(e => e.HasIndex(x => x.Name).IsUnique());
        b.Entity<UserRole>(e =>
        {
            e.HasKey(x => new { x.UserId, x.RoleId });
            e.HasOne(x => x.User).WithMany(u => u.UserRoles).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Role).WithMany(r => r.UserRoles).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<RefreshToken>(e =>
        {
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.Property(x => x.TokenHash).HasMaxLength(100);
            e.Property(x => x.ReplacedByTokenHash).HasMaxLength(100);
            e.HasOne(x => x.User).WithMany(u => u.RefreshTokens).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- master data ----------
        b.Entity<Customer>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => x.Name);
            e.Property(x => x.Code).HasMaxLength(20);
            e.Property(x => x.Address).HasMaxLength(300);
        });
        b.Entity<Supplier>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => x.Name);
            e.Property(x => x.Code).HasMaxLength(20);
            e.Property(x => x.Address).HasMaxLength(300);
        });
        b.Entity<Category>(e => e.HasIndex(x => x.Name).IsUnique());
        b.Entity<Warehouse>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(20);
        });
        b.Entity<Product>(e =>
        {
            e.HasIndex(x => x.Sku).IsUnique();
            e.HasIndex(x => x.Name);
            e.Property(x => x.Sku).HasMaxLength(50);
            e.Property(x => x.Unit).HasMaxLength(20);
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.CostPrice).HasPrecision(18, 4);
            e.Property(x => x.TaxRate).HasPrecision(5, 2);
            e.Property(x => x.QuantityOnHand).HasPrecision(18, 3);
            e.Property(x => x.ReorderLevel).HasPrecision(18, 3);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne(x => x.Category).WithMany(c => c.Products).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.PreferredSupplier).WithMany(s => s.Products).HasForeignKey(x => x.PreferredSupplierId).OnDelete(DeleteBehavior.SetNull);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Products_QuantityOnHand", "[QuantityOnHand] >= 0");
                t.HasCheckConstraint("CK_Products_Prices", "[CostPrice] >= 0 AND [SalePrice] >= 0");
            });
        });
        b.Entity<StockTransaction>(e =>
        {
            e.HasIndex(x => new { x.ProductId, x.TransactionDate });
            e.HasIndex(x => new { x.Type, x.TransactionDate });
            e.HasIndex(x => x.Reference);
            e.Property(x => x.Quantity).HasPrecision(18, 3);
            e.Property(x => x.UnitCost).HasPrecision(18, 4);
            e.Property(x => x.Reference).HasMaxLength(50);
            e.Property(x => x.Notes).HasMaxLength(300);
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- sales ----------
        b.Entity<SalesOrder>(e =>
        {
            e.HasIndex(x => x.OrderNumber).IsUnique();
            e.HasIndex(x => new { x.CustomerId, x.OrderDate });
            e.HasIndex(x => new { x.Status, x.OrderDate });
            e.Property(x => x.OrderNumber).HasMaxLength(30);
            e.Property(x => x.Notes).HasMaxLength(1000);
            e.HasOne(x => x.Customer).WithMany(c => c.SalesOrders).HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<SalesOrderDetail>(e =>
        {
            e.Property(x => x.Quantity).HasPrecision(18, 3);
            e.Property(x => x.TaxRate).HasPrecision(5, 2);
            e.HasOne(x => x.SalesOrder).WithMany(o => o.Details).HasForeignKey(x => x.SalesOrderId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("CK_SalesOrderDetails_Quantity", "[Quantity] > 0"));
        });
        b.Entity<Invoice>(e =>
        {
            e.HasIndex(x => x.InvoiceNumber).IsUnique();
            e.HasIndex(x => x.SalesOrderId).IsUnique();
            e.HasIndex(x => new { x.Status, x.DueDate });
            e.HasIndex(x => x.InvoiceDate);
            e.Property(x => x.InvoiceNumber).HasMaxLength(30);
            e.HasOne(x => x.SalesOrder).WithOne(o => o.Invoice).HasForeignKey<Invoice>(x => x.SalesOrderId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Customer).WithMany(c => c.Invoices).HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("CK_Invoices_AmountPaid", "[AmountPaid] >= 0 AND [AmountPaid] <= [TotalAmount]"));
        });

        // ---------- purchasing ----------
        b.Entity<PurchaseOrder>(e =>
        {
            e.HasIndex(x => x.OrderNumber).IsUnique();
            e.HasIndex(x => new { x.SupplierId, x.OrderDate });
            e.HasIndex(x => new { x.Status, x.ReceivedDate });
            e.Property(x => x.OrderNumber).HasMaxLength(30);
            e.Property(x => x.SupplierInvoiceNumber).HasMaxLength(50);
            e.Property(x => x.Notes).HasMaxLength(1000);
            e.HasOne(x => x.Supplier).WithMany(s => s.PurchaseOrders).HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("CK_PurchaseOrders_AmountPaid", "[AmountPaid] >= 0 AND [AmountPaid] <= [TotalAmount]"));
        });
        b.Entity<PurchaseOrderDetail>(e =>
        {
            e.Property(x => x.Quantity).HasPrecision(18, 3);
            e.Property(x => x.UnitCost).HasPrecision(18, 4);
            e.Property(x => x.TaxRate).HasPrecision(5, 2);
            e.HasOne(x => x.PurchaseOrder).WithMany(o => o.Details).HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("CK_PurchaseOrderDetails_Quantity", "[Quantity] > 0"));
        });

        // ---------- finance ----------
        b.Entity<Payment>(e =>
        {
            e.HasIndex(x => x.PaymentNumber).IsUnique();
            e.HasIndex(x => new { x.Direction, x.PaymentDate });
            e.Property(x => x.PaymentNumber).HasMaxLength(30);
            e.Property(x => x.Reference).HasMaxLength(100);
            e.Property(x => x.Notes).HasMaxLength(500);
            e.HasOne(x => x.Invoice).WithMany(i => i.Payments).HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.PurchaseOrder).WithMany(p => p.Payments).HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Payments_Amount", "[Amount] > 0");
                t.HasCheckConstraint("CK_Payments_Target", "([InvoiceId] IS NOT NULL AND [PurchaseOrderId] IS NULL) OR ([InvoiceId] IS NULL AND [PurchaseOrderId] IS NOT NULL)");
            });
        });
        b.Entity<Expense>(e =>
        {
            e.HasIndex(x => x.ExpenseDate);
            e.Property(x => x.Description).HasMaxLength(300);
            e.Property(x => x.Reference).HasMaxLength(100);
            e.ToTable(t => t.HasCheckConstraint("CK_Expenses_Amount", "[Amount] > 0"));
        });
    }
}
