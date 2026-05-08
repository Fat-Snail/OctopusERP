using Microsoft.EntityFrameworkCore;
using OctopusCRM.Api.Persistence;

namespace OctopusCRM.Api.Services;

public record InquiryListRow(
    long InquiryId,
    string InquiryCode,
    long CustomerId,
    string CustomerName,
    string Title,
    string Status,
    long AssignedTo,
    string? AssignedToName,
    DateTime CreatedAt);

public class InquiryService(CrmDbContext db)
{
    public async Task<(List<InquiryListRow> Rows, int Total)> GetListAsync(
        long? customerId, string? status, string? keyword, int page = 1, int size = 20)
    {
        var query = db.Inquiries.Include(x => x.Customer).AsQueryable();

        if (customerId.HasValue) query = query.Where(x => x.CustomerId == customerId.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(x => x.Title.Contains(keyword) || x.InquiryCode.Contains(keyword));

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        var rows = items.Select(x => new InquiryListRow(
            x.InquiryId, x.InquiryCode, x.CustomerId,
            x.Customer?.CustomerName ?? string.Empty,
            x.Title, x.Status, x.AssignedTo, x.AssignedToName, x.CreatedAt)).ToList();

        return (rows, total);
    }

    public async Task<CrmInquiry?> GetByIdAsync(long id)
    {
        return await db.Inquiries
            .Include(x => x.Customer)
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.InquiryId == id);
    }

    public async Task<CrmInquiry> CreateAsync(
        long customerId, string title, string? description,
        DateTime? expectedDelivery, long assignedTo, string? assignedToName, long createdBy)
    {
        var code = await GenerateCodeAsync();
        var now = DateTime.UtcNow;
        var inquiry = new CrmInquiry
        {
            InquiryCode = code,
            CustomerId = customerId,
            Title = title,
            Description = description,
            Status = "open",
            ExpectedDelivery = expectedDelivery,
            AssignedTo = assignedTo,
            AssignedToName = assignedToName,
            CreatedBy = createdBy,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Inquiries.Add(inquiry);
        await db.SaveChangesAsync();
        return inquiry;
    }

    public async Task<string?> UpdateAsync(
        long id, string? title, string? description,
        DateTime? expectedDelivery, long? assignedTo, string? assignedToName)
    {
        var inquiry = await db.Inquiries.FindAsync(id);
        if (inquiry == null) return "询盘不存在";

        if (!string.IsNullOrWhiteSpace(title)) inquiry.Title = title;
        if (description != null) inquiry.Description = description;
        if (expectedDelivery.HasValue) inquiry.ExpectedDelivery = expectedDelivery;
        if (assignedTo.HasValue) inquiry.AssignedTo = assignedTo.Value;
        if (assignedToName != null) inquiry.AssignedToName = assignedToName;
        inquiry.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return null;
    }

    public async Task<string?> DeleteAsync(long id)
    {
        var inquiry = await db.Inquiries.FindAsync(id);
        if (inquiry == null) return "询盘不存在";

        var hasQuote = await db.Quotes.AnyAsync(x => x.InquiryId == id);
        if (hasQuote) return "该询盘已有报价单，无法删除";

        db.Inquiries.Remove(inquiry);
        await db.SaveChangesAsync();
        return null;
    }

    public async Task<CrmInquiryItem> AddItemAsync(
        long inquiryId, string productName, string? spec, decimal quantity,
        string? unit, long? plmProductId, string? remark)
    {
        var item = new CrmInquiryItem
        {
            InquiryId = inquiryId,
            ProductName = productName,
            Spec = spec,
            Quantity = quantity,
            Unit = unit,
            PlmProductId = plmProductId,
            Remark = remark
        };
        db.InquiryItems.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    public async Task<string?> UpdateItemAsync(
        long itemId, string? productName, string? spec, decimal? quantity, string? unit, string? remark)
    {
        var item = await db.InquiryItems.FindAsync(itemId);
        if (item == null) return "明细不存在";

        if (!string.IsNullOrWhiteSpace(productName)) item.ProductName = productName;
        if (spec != null) item.Spec = spec;
        if (quantity.HasValue) item.Quantity = quantity.Value;
        if (unit != null) item.Unit = unit;
        if (remark != null) item.Remark = remark;

        await db.SaveChangesAsync();
        return null;
    }

    public async Task<string?> DeleteItemAsync(long itemId)
    {
        var item = await db.InquiryItems.FindAsync(itemId);
        if (item == null) return "明细不存在";

        db.InquiryItems.Remove(item);
        await db.SaveChangesAsync();
        return null;
    }

    public async Task<string?> UpdateStatusAsync(long id, string newStatus)
    {
        var inquiry = await db.Inquiries.FindAsync(id);
        if (inquiry == null) return "询盘不存在";

        var validTransitions = new Dictionary<string, string[]>
        {
            ["open"] = new[] { "quoted", "won", "lost" }
        };

        if (!validTransitions.TryGetValue(inquiry.Status, out var allowed) ||
            !allowed.Contains(newStatus))
            return $"无法从 {inquiry.Status} 转换到 {newStatus}";

        inquiry.Status = newStatus;
        inquiry.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return null;
    }

    private async Task<string> GenerateCodeAsync()
    {
        var today = DateTime.UtcNow.ToString("yyyyMMdd");
        var prefix = $"INQ-{today}-";
        var maxSeq = await db.Inquiries
            .Where(x => x.InquiryCode.StartsWith(prefix))
            .Select(x => x.InquiryCode)
            .ToListAsync();

        int seq = 1;
        if (maxSeq.Any())
        {
            var nums = maxSeq
                .Select(c => int.TryParse(c.Substring(prefix.Length), out var n) ? n : 0)
                .Where(n => n > 0);
            if (nums.Any()) seq = nums.Max() + 1;
        }
        return $"{prefix}{seq:D3}";
    }
}
