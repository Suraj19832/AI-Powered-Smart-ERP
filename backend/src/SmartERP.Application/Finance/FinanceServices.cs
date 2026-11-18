using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using SmartERP.Application.Common;
using SmartERP.Domain.Entities;
using SmartERP.Domain.Enums;

namespace SmartERP.Application.Finance;

// ---------- Payments ----------

public class PaymentInput
{
    public PaymentDirection Direction { get; set; }
    /// <summary>Required for incoming payments.</summary>
    public int? InvoiceId { get; set; }
    /// <summary>Required for outgoing payments.</summary>
    public int? PurchaseOrderId { get; set; }
    [Range(0.01, 100_000_000)] public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public DateTime? PaymentDate { get; set; }
    [MaxLength(100)] public string? Reference { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
}

public class PaymentQuery : PagedQuery
{
    public PaymentDirection? Direction { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}

public record PaymentDto(int Id, string PaymentNumber, DateTime PaymentDate, string Direction, string Method, decimal Amount,
    string? Reference, string? Notes, int? InvoiceId, string? InvoiceNumber, int? PurchaseOrderId, string? PurchaseOrderNumber, string PartyName);

public class PaymentService(IAppDbContext db)
{
    public async Task<PagedResult<PaymentDto>> ListAsync(PaymentQuery q, CancellationToken ct)
    {
        var query = db.Payments.AsNoTracking();
        if (q.Direction.HasValue) query = query.Where(p => p.Direction == q.Direction);
        if (q.From.HasValue) query = query.Where(p => p.PaymentDate >= q.From);
        if (q.To.HasValue) query = query.Where(p => p.PaymentDate < q.To.Value.Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(q.Search))
            query = query.Where(p => p.PaymentNumber.Contains(q.Search) || (p.Reference != null && p.Reference.Contains(q.Search)) ||
                                     (p.Invoice != null && p.Invoice.Customer.Name.Contains(q.Search)) ||
                                     (p.PurchaseOrder != null && p.PurchaseOrder.Supplier.Name.Contains(q.Search)));
        return await query.ApplySort(q.SortBy, q.Desc, "PaymentDate", true).ToPagedAsync(q, p => new PaymentDto(
            p.Id, p.PaymentNumber, p.PaymentDate, p.Direction.ToString(), p.Method.ToString(), p.Amount, p.Reference, p.Notes,
            p.InvoiceId, p.Invoice != null ? p.Invoice.InvoiceNumber : null,
            p.PurchaseOrderId, p.PurchaseOrder != null ? p.PurchaseOrder.OrderNumber : null,
            p.Invoice != null ? p.Invoice.Customer.Name : p.PurchaseOrder != null ? p.PurchaseOrder.Supplier.Name : ""), ct);
    }

    /// <summary>Records a customer receipt or supplier payment and updates the document balance atomically.</summary>
    public async Task<int> RecordAsync(PaymentInput input, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var date = input.PaymentDate ?? DateTime.UtcNow;
        var payment = new Payment
        {
            Direction = input.Direction, Amount = input.Amount, Method = input.Method, PaymentDate = date,
            Reference = input.Reference, Notes = input.Notes
        };

        if (input.Direction == PaymentDirection.Incoming)
        {
            if (input.InvoiceId is null) throw new BusinessRuleException("Select an invoice for a customer payment.");
            var inv = await db.Invoices.FirstOrDefaultAsync(i => i.Id == input.InvoiceId, ct) ?? throw new NotFoundException("Invoice", input.InvoiceId);
            if (inv.Status is InvoiceStatus.Cancelled or InvoiceStatus.Paid) throw new BusinessRuleException($"Invoice is {inv.Status}.");
            var balance = inv.TotalAmount - inv.AmountPaid;
            if (input.Amount > balance) throw new BusinessRuleException($"Amount exceeds balance due ({balance:N2}).");
            inv.AmountPaid += input.Amount;
            inv.Status = inv.AmountPaid >= inv.TotalAmount ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid;
            inv.UpdatedAt = DateTime.UtcNow;
            payment.InvoiceId = inv.Id;
        }
        else
        {
            if (input.PurchaseOrderId is null) throw new BusinessRuleException("Select a purchase order for a supplier payment.");
            var po = await db.PurchaseOrders.FirstOrDefaultAsync(p => p.Id == input.PurchaseOrderId, ct) ?? throw new NotFoundException("Purchase order", input.PurchaseOrderId);
            if (po.Status == PurchaseOrderStatus.Cancelled) throw new BusinessRuleException("Purchase order is cancelled.");
            var balance = po.TotalAmount - po.AmountPaid;
            if (input.Amount > balance) throw new BusinessRuleException($"Amount exceeds balance due ({balance:N2}).");
            po.AmountPaid += input.Amount;
            po.UpdatedAt = DateTime.UtcNow;
            payment.PurchaseOrderId = po.Id;
        }

        payment.PaymentNumber = await DocumentNumbers.NextAsync(db.Payments.Select(p => p.PaymentNumber),
            input.Direction == PaymentDirection.Incoming ? "RCV" : "PAY", date, ct);
        db.Payments.Add(payment);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return payment.Id;
    }
}

// ---------- Expenses ----------

public class ExpenseInput
{
    public DateTime? ExpenseDate { get; set; }
    public ExpenseCategory Category { get; set; }
    [Required, MaxLength(300)] public string Description { get; set; } = "";
    [Range(0.01, 100_000_000)] public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    [MaxLength(100)] public string? Reference { get; set; }
}

public class ExpenseQuery : PagedQuery
{
    public ExpenseCategory? Category { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}

public record ExpenseDto(int Id, DateTime ExpenseDate, string Category, string Description, decimal Amount, string Method, string? Reference);

public class ExpenseService(IAppDbContext db)
{
    public async Task<PagedResult<ExpenseDto>> ListAsync(ExpenseQuery q, CancellationToken ct)
    {
        var query = db.Expenses.AsNoTracking();
        if (q.Category.HasValue) query = query.Where(e => e.Category == q.Category);
        if (q.From.HasValue) query = query.Where(e => e.ExpenseDate >= q.From);
        if (q.To.HasValue) query = query.Where(e => e.ExpenseDate < q.To.Value.Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(q.Search)) query = query.Where(e => e.Description.Contains(q.Search) || (e.Reference != null && e.Reference.Contains(q.Search)));
        return await query.ApplySort(q.SortBy, q.Desc, "ExpenseDate", true).ToPagedAsync(q, e => new ExpenseDto(
            e.Id, e.ExpenseDate, e.Category.ToString(), e.Description, e.Amount, e.Method.ToString(), e.Reference), ct);
    }

    public async Task<int> CreateAsync(ExpenseInput i, CancellationToken ct)
    {
        var e = new Expense();
        Map(i, e);
        db.Expenses.Add(e);
        await db.SaveChangesAsync(ct);
        return e.Id;
    }

    public async Task UpdateAsync(int id, ExpenseInput i, CancellationToken ct)
    {
        var e = await db.Expenses.FindAsync([id], ct) ?? throw new NotFoundException("Expense", id);
        Map(i, e);
        e.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var e = await db.Expenses.FindAsync([id], ct) ?? throw new NotFoundException("Expense", id);
        db.Expenses.Remove(e);
        await db.SaveChangesAsync(ct);
    }

    private static void Map(ExpenseInput i, Expense e)
    {
        e.ExpenseDate = i.ExpenseDate ?? DateTime.UtcNow; e.Category = i.Category; e.Description = i.Description.Trim();
        e.Amount = i.Amount; e.Method = i.Method; e.Reference = i.Reference;
    }
}
