using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace SmartERP.Application.Common;

public class PagedQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public bool Desc { get; set; }
}

public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

public static class QueryableExtensions
{
    /// <summary>Sort by a whitelisted scalar property name of the entity (unknown names are ignored).</summary>
    public static IQueryable<T> ApplySort<T>(this IQueryable<T> query, string? sortBy, bool desc, string defaultSort = "Id", bool defaultDesc = true)
    {
        var prop = Resolve<T>(sortBy);
        if (prop == null)
        {
            prop = Resolve<T>(defaultSort)!;
            desc = defaultDesc;
        }
        return desc
            ? query.OrderByDescending(e => EF.Property<object>(e!, prop.Name))
            : query.OrderBy(e => EF.Property<object>(e!, prop.Name));
    }

    private static PropertyInfo? Resolve<T>(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var p = typeof(T).GetProperty(name, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
        if (p == null) return null;
        var t = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
        return t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal) || t == typeof(DateTime) ? p : null;
    }

    public static async Task<PagedResult<TOut>> ToPagedAsync<TIn, TOut>(
        this IQueryable<TIn> query, PagedQuery q, System.Linq.Expressions.Expression<Func<TIn, TOut>> selector, CancellationToken ct)
    {
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 1, 200);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * size).Take(size).Select(selector).ToListAsync(ct);
        return new PagedResult<TOut>(items, total, page, size);
    }
}
