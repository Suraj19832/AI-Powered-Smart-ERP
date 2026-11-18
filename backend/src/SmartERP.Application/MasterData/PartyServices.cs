using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using SmartERP.Application.Common;
using SmartERP.Domain.Entities;
using SmartERP.Domain.Enums;

namespace SmartERP.Application.MasterData;

// ---------- Customers ----------

public class CustomerInput
{
    [Required, MaxLength(150)] public string Name { get; set; } = "";
    [EmailAddress, MaxLength(150)] public string? Email { get; set; }
    [MaxLength(30)] public string? Phone { get; set; }
    [MaxLength(300)] public string? Address { get; set; }
    [MaxLength(80)] public string? City { get; set; }
    [MaxLength(30)] public string? TaxNumber { get; set; }
    [Range(0, 100_000_000)] public decimal CreditLimit { get; set; }
    public bool IsActive { get; set; } = true;
}

public record CustomerDto(int Id, string Code, string Name, string? Email, string? Phone, string? Address, string? City,
    string? TaxNumber, decimal CreditLimit, bool IsActive, decimal OutstandingBalance, DateTime CreatedAt);

public record PartyTransactionDto(int Id, string Type, string Number, DateTime Date, decimal Amount, string Status);

public record CustomerDetailDto(CustomerDto Customer, decimal TotalSales, decimal TotalPaid, int OrderCount,
    IReadOnlyList<PartyTransactionDto> Transactions);

public class CustomerService(IAppDbContext db)
{
    public async Task<PagedResult<CustomerDto>> ListAsync(PagedQuery q, CancellationToken ct)
    {
        var query = db.Customers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.Search))
            query = query.Where(c => c.Name.Contains(q.Search) || c.Code.Contains(q.Search) || (c.Email != null && c.Email.Contains(q.Search)) || (c.City != null && c.City.Contains(q.Search)));
        return await query.ApplySort(q.SortBy, q.Desc, "Name", false).ToPagedAsync(q, c => new CustomerDto(
            c.Id, c.Code, c.Name, c.Email, c.Phone, c.Address, c.City, c.TaxNumber, c.CreditLimit, c.IsActive,
            c.Invoices.Where(i => i.Status != InvoiceStatus.Cancelled).Sum(i => (decimal?)(i.TotalAmount - i.AmountPaid)) ?? 0,
            c.CreatedAt), ct);
    }

    public Task<List<Inventory.LookupDto>> LookupAsync(CancellationToken ct) =>
        db.Customers.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.Name)
            .Select(c => new Inventory.LookupDto(c.Id, c.Name, c.Code, null, null, null)).ToListAsync(ct);

    public async Task<CustomerDetailDto> GetAsync(int id, CancellationToken ct)
    {
        var c = await db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Customer", id);
        var invoices = await db.Invoices.AsNoTracking().Where(i => i.CustomerId == id && i.Status != InvoiceStatus.Cancelled)
            .Select(i => new { i.TotalAmount, i.AmountPaid }).ToListAsync(ct);

        var orders = await db.SalesOrders.AsNoTracking().Where(o => o.CustomerId == id)
            .Select(o => new PartyTransactionDto(o.Id, "Sales Order", o.OrderNumber, o.OrderDate, o.TotalAmount, o.Status.ToString())).ToListAsync(ct);
        var payments = await db.Payments.AsNoTracking().Where(p => p.Invoice != null && p.Invoice.CustomerId == id)
            .Select(p => new PartyTransactionDto(p.Id, "Payment", p.PaymentNumber, p.PaymentDate, p.Amount, p.Method.ToString())).ToListAsync(ct);

        var outstanding = invoices.Sum(i => i.TotalAmount - i.AmountPaid);
        var dto = new CustomerDto(c.Id, c.Code, c.Name, c.Email, c.Phone, c.Address, c.City, c.TaxNumber, c.CreditLimit, c.IsActive, outstanding, c.CreatedAt);
        return new CustomerDetailDto(dto, invoices.Sum(i => i.TotalAmount), invoices.Sum(i => i.AmountPaid), orders.Count,
            orders.Concat(payments).OrderByDescending(t => t.Date).Take(100).ToList());
    }

    public async Task<int> CreateAsync(CustomerInput input, CancellationToken ct)
    {
        var entity = new Customer { Code = await NextCode(ct) };
        Map(input, entity);
        db.Customers.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity.Id;
    }

    public async Task UpdateAsync(int id, CustomerInput input, CancellationToken ct)
    {
        var entity = await db.Customers.FindAsync([id], ct) ?? throw new NotFoundException("Customer", id);
        Map(input, entity);
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var entity = await db.Customers.FindAsync([id], ct) ?? throw new NotFoundException("Customer", id);
        if (await db.SalesOrders.AnyAsync(o => o.CustomerId == id, ct))
            throw new BusinessRuleException("Customer has sales orders and cannot be deleted. Mark it inactive instead.");
        db.Customers.Remove(entity);
        await db.SaveChangesAsync(ct);
    }

    private async Task<string> NextCode(CancellationToken ct)
    {
        var max = await db.Customers.MaxAsync(c => (int?)c.Id, ct) ?? 0;
        return $"CUS{max + 1:D4}";
    }

    private static void Map(CustomerInput i, Customer e)
    {
        e.Name = i.Name.Trim(); e.Email = i.Email; e.Phone = i.Phone; e.Address = i.Address; e.City = i.City;
        e.TaxNumber = i.TaxNumber; e.CreditLimit = i.CreditLimit; e.IsActive = i.IsActive;
    }
}

// ---------- Suppliers ----------

public class SupplierInput
{
    [Required, MaxLength(150)] public string Name { get; set; } = "";
    [MaxLength(100)] public string? ContactPerson { get; set; }
    [EmailAddress, MaxLength(150)] public string? Email { get; set; }
    [MaxLength(30)] public string? Phone { get; set; }
    [MaxLength(300)] public string? Address { get; set; }
    [MaxLength(80)] public string? City { get; set; }
    [MaxLength(30)] public string? TaxNumber { get; set; }
    [Range(0, 365)] public int LeadTimeDays { get; set; } = 7;
    public bool IsActive { get; set; } = true;
}

public record SupplierDto(int Id, string Code, string Name, string? ContactPerson, string? Email, string? Phone, string? Address,
    string? City, string? TaxNumber, int LeadTimeDays, bool IsActive, decimal PayableBalance, DateTime CreatedAt);

public record SupplierDetailDto(SupplierDto Supplier, decimal TotalPurchases, decimal TotalPaid, int OrderCount,
    IReadOnlyList<PartyTransactionDto> Transactions);

public class SupplierService(IAppDbContext db)
{
    public async Task<PagedResult<SupplierDto>> ListAsync(PagedQuery q, CancellationToken ct)
    {
        var query = db.Suppliers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.Search))
            query = query.Where(s => s.Name.Contains(q.Search) || s.Code.Contains(q.Search) || (s.ContactPerson != null && s.ContactPerson.Contains(q.Search)));
        return await query.ApplySort(q.SortBy, q.Desc, "Name", false).ToPagedAsync(q, s => new SupplierDto(
            s.Id, s.Code, s.Name, s.ContactPerson, s.Email, s.Phone, s.Address, s.City, s.TaxNumber, s.LeadTimeDays, s.IsActive,
            s.PurchaseOrders.Where(p => p.Status == PurchaseOrderStatus.Received).Sum(p => (decimal?)(p.TotalAmount - p.AmountPaid)) ?? 0,
            s.CreatedAt), ct);
    }

    public Task<List<Inventory.LookupDto>> LookupAsync(CancellationToken ct) =>
        db.Suppliers.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Name)
            .Select(s => new Inventory.LookupDto(s.Id, s.Name, s.Code, null, null, null)).ToListAsync(ct);

    public async Task<SupplierDetailDto> GetAsync(int id, CancellationToken ct)
    {
        var s = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Supplier", id);
        var orders = await db.PurchaseOrders.AsNoTracking().Where(o => o.SupplierId == id)
            .Select(o => new { o.Id, o.OrderNumber, o.OrderDate, o.TotalAmount, o.AmountPaid, o.Status }).ToListAsync(ct);
        var payments = await db.Payments.AsNoTracking().Where(p => p.PurchaseOrder != null && p.PurchaseOrder.SupplierId == id)
            .Select(p => new PartyTransactionDto(p.Id, "Payment", p.PaymentNumber, p.PaymentDate, p.Amount, p.Method.ToString())).ToListAsync(ct);

        var received = orders.Where(o => o.Status == PurchaseOrderStatus.Received).ToList();
        var payable = received.Sum(o => o.TotalAmount - o.AmountPaid);
        var dto = new SupplierDto(s.Id, s.Code, s.Name, s.ContactPerson, s.Email, s.Phone, s.Address, s.City, s.TaxNumber, s.LeadTimeDays, s.IsActive, payable, s.CreatedAt);
        var tx = orders.Select(o => new PartyTransactionDto(o.Id, "Purchase Order", o.OrderNumber, o.OrderDate, o.TotalAmount, o.Status.ToString()))
            .Concat(payments).OrderByDescending(t => t.Date).Take(100).ToList();
        return new SupplierDetailDto(dto, received.Sum(o => o.TotalAmount), received.Sum(o => o.AmountPaid), orders.Count, tx);
    }

    public async Task<int> CreateAsync(SupplierInput input, CancellationToken ct)
    {
        var max = await db.Suppliers.MaxAsync(c => (int?)c.Id, ct) ?? 0;
        var entity = new Supplier { Code = $"SUP{max + 1:D4}" };
        Map(input, entity);
        db.Suppliers.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity.Id;
    }

    public async Task UpdateAsync(int id, SupplierInput input, CancellationToken ct)
    {
        var entity = await db.Suppliers.FindAsync([id], ct) ?? throw new NotFoundException("Supplier", id);
        Map(input, entity);
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var entity = await db.Suppliers.FindAsync([id], ct) ?? throw new NotFoundException("Supplier", id);
        if (await db.PurchaseOrders.AnyAsync(o => o.SupplierId == id, ct))
            throw new BusinessRuleException("Supplier has purchase orders and cannot be deleted. Mark it inactive instead.");
        await db.Products.Where(p => p.PreferredSupplierId == id).ExecuteUpdateAsync(s => s.SetProperty(p => p.PreferredSupplierId, (int?)null), ct);
        db.Suppliers.Remove(entity);
        await db.SaveChangesAsync(ct);
    }

    private static void Map(SupplierInput i, Supplier e)
    {
        e.Name = i.Name.Trim(); e.ContactPerson = i.ContactPerson; e.Email = i.Email; e.Phone = i.Phone; e.Address = i.Address;
        e.City = i.City; e.TaxNumber = i.TaxNumber; e.LeadTimeDays = i.LeadTimeDays; e.IsActive = i.IsActive;
    }
}
