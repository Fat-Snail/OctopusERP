using Microsoft.EntityFrameworkCore;
using OctopusCRM.Api.Persistence;

namespace OctopusCRM.Api.Services;

public record QuoteListRow(
    long QuoteId,
    string QuoteCode,
    long InquiryId,
    long CustomerId,
    string CustomerName,
    string Status,
    decimal TotalAmount,
    string Currency,
    DateTime CreatedAt);

public class QuoteService(CrmDbContext db)
{
    public async Task<(List<QuoteListRow> Rows, int Total)> GetListAsync(
        long? customerId, long? inquiryId, string? status, int page = 1, int size = 20)
    {
        var query = db.Quotes
            .Include(x => x.Inquiry).ThenInclude(i => i!.Customer)
            .AsQueryable();

        if (customerId.HasValue) query = query.Where(x => x.CustomerId == customerId.Value);
        if (inquiryId.HasValue) query = query.Where(x => x.InquiryId == inquiryId.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        var rows = items.Select(x => new QuoteListRow(
            x.QuoteId, x.QuoteCode, x.InquiryId, x.CustomerId,
            x.Inquiry?.Customer?.CustomerName ?? string.Empty,
            x.Status, x.TotalAmount, x.Currency, x.CreatedAt)).ToList();

        return (rows, total);
    }

    public async Task<CrmQuote?> GetByIdAsync(long id)
    {
        return await db.Quotes
            .Include(x => x.Inquiry).ThenInclude(i => i!.Customer)
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.QuoteId == id);
    }

    public async Task<CrmQuote> CreateAsync(
        long inquiryId, long customerId, string? currency, DateTime? validUntil,
        DateTime? expectedDelivery, string? terms, string? remark, long createdBy)
    {
        var code = await GenerateCodeAsync();
        var now = DateTime.UtcNow;
        var quote = new CrmQuote
        {
            QuoteCode = code,
            InquiryId = inquiryId,
            CustomerId = customerId,
            Status = "draft",
            TotalAmount = 0m,
            Currency = currency ?? "CNY",
            ValidUntil = validUntil,
            ExpectedDelivery = expectedDelivery,
            Terms = terms,
            Remark = remark,
            CreatedBy = createdBy,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Quotes.Add(quote);
        await db.SaveChangesAsync();
        return quote;
    }

    public async Task<string?> UpdateAsync(
        long id, string? currency, DateTime? validUntil, DateTime? expectedDelivery,
        string? terms, string? remark)
    {
        var quote = await db.Quotes.FindAsync(id);
        if (quote == null) return "报价单不存在";
        if (quote.Status != "draft") return "仅草稿状态可修改";

        if (!string.IsNullOrWhiteSpace(currency)) quote.Currency = currency;
        if (validUntil.HasValue) quote.ValidUntil = validUntil;
        if (expectedDelivery.HasValue) quote.ExpectedDelivery = expectedDelivery;
        if (terms != null) quote.Terms = terms;
        if (remark != null) quote.Remark = remark;
        quote.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return null;
    }

    public async Task<string?> DeleteAsync(long id)
    {
        var quote = await db.Quotes.FindAsync(id);
        if (quote == null) return "报价单不存在";
        if (quote.Status != "draft") return "仅草稿状态可删除";

        db.Quotes.Remove(quote);
        await db.SaveChangesAsync();
        return null;
    }

    public async Task<CrmQuoteItem> AddItemAsync(
        long quoteId, string productName, decimal quantity, decimal unitPrice,
        string? spec, string? unit, long? plmProductId, string? remark)
    {
        var item = new CrmQuoteItem
        {
            QuoteId = quoteId,
            ProductName = productName,
            Spec = spec,
            Quantity = quantity,
            Unit = unit,
            UnitPrice = unitPrice,
            Amount = quantity * unitPrice,
            PlmProductId = plmProductId,
            Remark = remark
        };
        db.QuoteItems.Add(item);

        await UpdateTotalAmount(quoteId, item.Amount);
        await db.SaveChangesAsync();
        return item;
    }

    public async Task<string?> UpdateItemAsync(
        long itemId, string? productName, decimal? quantity, decimal? unitPrice,
        string? spec, string? unit, string? remark)
    {
        var item = await db.QuoteItems.FindAsync(itemId);
        if (item == null) return "明细不存在";

        var oldAmount = item.Amount;
        if (!string.IsNullOrWhiteSpace(productName)) item.ProductName = productName;
        if (spec != null) item.Spec = spec;
        if (quantity.HasValue) item.Quantity = quantity.Value;
        if (unitPrice.HasValue) item.UnitPrice = unitPrice.Value;
        if (unit != null) item.Unit = unit;
        if (remark != null) item.Remark = remark;
        item.Amount = item.Quantity * item.UnitPrice;

        await db.SaveChangesAsync();
        await RecalcTotalAmountAsync(item.QuoteId);
        return null;
    }

    public async Task<string?> DeleteItemAsync(long itemId)
    {
        var item = await db.QuoteItems.FindAsync(itemId);
        if (item == null) return "明细不存在";

        var quoteId = item.QuoteId;
        db.QuoteItems.Remove(item);
        await db.SaveChangesAsync();
        await RecalcTotalAmountAsync(quoteId);
        return null;
    }

    public async Task<(long OaApprovalId, string? Error)> SubmitAsync(long id)
    {
        var quote = await db.Quotes.FindAsync(id);
        if (quote == null) return (0, "报价单不存在");
        if (quote.Status != "draft") return (0, "仅草稿状态可提交审批");

        // 用 quoteId 生成唯一的 mock 审批ID，实际应调 OA 接口
        var mockApprovalId = quote.QuoteId + 100000L;
        quote.Status = "pending_approval";
        quote.OaApprovalId = mockApprovalId;
        quote.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return (mockApprovalId, null);
    }

    public async Task<string?> ApprovalCallbackAsync(long oaApprovalId, bool approved)
    {
        var quote = await db.Quotes.FirstOrDefaultAsync(x => x.OaApprovalId == oaApprovalId);
        if (quote == null) return null; // 不是报价单的审批，忽略

        quote.Status = approved ? "approved" : "rejected";
        quote.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return null;
    }

    public async Task<string?> ConfirmAsync(long id)
    {
        var quote = await db.Quotes
            .Include(x => x.Inquiry)
            .FirstOrDefaultAsync(x => x.QuoteId == id);
        if (quote == null) return "报价单不存在";
        if (quote.Status != "approved") return "仅审批通过的报价单可确认";

        quote.Status = "confirmed";
        quote.UpdatedAt = DateTime.UtcNow;

        if (quote.Inquiry != null && quote.Inquiry.Status == "open")
        {
            quote.Inquiry.Status = "quoted";
            quote.Inquiry.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
        return null;
    }

    private async Task UpdateTotalAmount(long quoteId, decimal addAmount)
    {
        var quote = await db.Quotes.FindAsync(quoteId);
        if (quote != null)
        {
            quote.TotalAmount += addAmount;
            quote.UpdatedAt = DateTime.UtcNow;
        }
    }

    private async Task RecalcTotalAmountAsync(long quoteId)
    {
        var quote = await db.Quotes.FindAsync(quoteId);
        if (quote == null) return;

        var total = await db.QuoteItems
            .Where(x => x.QuoteId == quoteId)
            .SumAsync(x => x.Amount);
        quote.TotalAmount = total;
        quote.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    private async Task<string> GenerateCodeAsync()
    {
        var today = DateTime.UtcNow.ToString("yyyyMMdd");
        var prefix = $"QUO-{today}-";
        var maxSeq = await db.Quotes
            .Where(x => x.QuoteCode.StartsWith(prefix))
            .Select(x => x.QuoteCode)
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
