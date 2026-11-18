using Microsoft.EntityFrameworkCore;

namespace SmartERP.Application.Common;

/// <summary>Generates sequential document numbers like SO-202611-0007. Unique indexes in the DB guard against collisions.</summary>
public static class DocumentNumbers
{
    public static async Task<string> NextAsync(IQueryable<string> existingNumbers, string prefix, DateTime date, CancellationToken ct)
    {
        var stem = $"{prefix}-{date:yyyyMM}-";
        var last = await existingNumbers.Where(n => n.StartsWith(stem)).OrderByDescending(n => n).FirstOrDefaultAsync(ct);
        var next = last != null && int.TryParse(last[stem.Length..], out var n) ? n + 1 : 1;
        return $"{stem}{next:D4}";
    }
}
