using Microsoft.EntityFrameworkCore;
using OctopusCRM.Api.Persistence;

namespace OctopusCRM.Api.Services;

public record ContractListRow(
    long ContractId,
    string ContractCode,
    long CustomerId,
    string CustomerName,
    string Title,
    string Status,
    decimal TotalAmount,
    string Currency,
    DateTime? DeliveryDate,
    DateTime CreatedAt);

public class ContractService(CrmDbContext db)
{
    public async Task<(List<ContractListRow> Rows, int Total)> GetListAsync(
        long? customerId, string? status, int page = 1, int size = 20)
    {
        var query = db.Contracts
            .Include(x => x.Quote).ThenInclude(q => q!.Inquiry).ThenInclude(i => i!.Customer)
            .AsQueryable();

        if (customerId.HasValue) query = query.Where(x => x.CustomerId == customerId.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        var customerIds = items.Select(x => x.CustomerId).Distinct().ToList();
        var customers = await db.Customers
            .Where(x => customerIds.Contains(x.CustomerId))
            .ToDictionaryAsync(x => x.CustomerId, x => x.CustomerName);

        var rows = items.Select(x => new ContractListRow(
            x.ContractId, x.ContractCode, x.CustomerId,
            customers.TryGetValue(x.CustomerId, out var cn) ? cn : string.Empty,
            x.Title, x.Status, x.TotalAmount, x.Currency,
            x.DeliveryDate, x.CreatedAt)).ToList();

        return (rows, total);
    }

    public async Task<CrmContract?> GetByIdAsync(long id)
    {
        return await db.Contracts
            .Include(x => x.Quote).ThenInclude(q => q!.Inquiry)
            .Include(x => x.Items)
            .Include(x => x.Payments)
            .FirstOrDefaultAsync(x => x.ContractId == id);
    }

    public async Task<(CrmContract? Contract, string? Error)> CreateFromQuoteAsync(
        long quoteId, string title, DateTime? deliveryDate, DateTime? signDate,
        string? fileUrl, string? remark, long createdBy)
    {
        var quote = await db.Quotes
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.QuoteId == quoteId);

        if (quote == null) return (null, "报价单不存在");
        if (quote.Status != "confirmed") return (null, "仅已确认的报价单可创建合同");

        var code = await GenerateCodeAsync();
        var now = DateTime.UtcNow;

        var contract = new CrmContract
        {
            ContractCode = code,
            QuoteId = quoteId,
            CustomerId = quote.CustomerId,
            Title = title,
            TotalAmount = quote.TotalAmount,
            Currency = quote.Currency,
            SignDate = signDate,
            DeliveryDate = deliveryDate,
            Status = "draft",
            FileUrl = fileUrl,
            Remark = remark,
            CreatedBy = createdBy,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Contracts.Add(contract);
        await db.SaveChangesAsync();

        // 复制报价明细到合同明细
        foreach (var qi in quote.Items)
        {
            db.ContractItems.Add(new CrmContractItem
            {
                ContractId = contract.ContractId,
                PlmProductId = qi.PlmProductId,
                ProductName = qi.ProductName,
                Quantity = qi.Quantity,
                Unit = qi.Unit,
                UnitPrice = qi.UnitPrice,
                Amount = qi.Amount
            });
        }
        await db.SaveChangesAsync();

        return (contract, null);
    }

    public async Task<string?> UpdateAsync(long id, string? title, DateTime? deliveryDate, string? remark)
    {
        var contract = await db.Contracts.FindAsync(id);
        if (contract == null) return "合同不存在";
        if (contract.Status != "draft") return "仅草稿状态可修改";

        if (!string.IsNullOrWhiteSpace(title)) contract.Title = title;
        if (deliveryDate.HasValue) contract.DeliveryDate = deliveryDate;
        if (remark != null) contract.Remark = remark;
        contract.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return null;
    }

    public async Task<(long OaApprovalId, string? Error)> SubmitAsync(long id)
    {
        var contract = await db.Contracts.FindAsync(id);
        if (contract == null) return (0, "合同不存在");
        if (contract.Status != "draft") return (0, "仅草稿状态可提交审批");

        // 用 contractId 生成唯一的 mock 审批ID，实际应调 OA 接口
        var mockApprovalId = contract.ContractId + 200000L;
        contract.Status = "pending_approval";
        contract.OaApprovalId = mockApprovalId;
        contract.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return (mockApprovalId, null);
    }

    public async Task<string?> ApprovalCallbackAsync(long oaApprovalId, bool approved)
    {
        var contract = await db.Contracts.FirstOrDefaultAsync(x => x.OaApprovalId == oaApprovalId);
        if (contract == null) return null;

        if (approved)
        {
            contract.Status = "active";
        }
        else
        {
            contract.Status = "draft";
            contract.OaApprovalId = null;
        }
        contract.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return null;
    }

    public async Task<string?> ExecuteAsync(long id)
    {
        var contract = await db.Contracts.FindAsync(id);
        if (contract == null) return "合同不存在";
        if (contract.Status != "pending_approval") return "仅待审批状态可执行（设为激活）";

        contract.Status = "active";
        contract.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return null;
    }

    public async Task<string?> ShipAsync(long id, string? trackingNumber, DateTime? actualDeliveryDate)
    {
        var contract = await db.Contracts.FindAsync(id);
        if (contract == null) return "合同不存在";
        if (contract.Status != "active") return "仅激活状态可发货";

        contract.Status = "shipped";
        if (actualDeliveryDate.HasValue) contract.ActualDeliveryDate = actualDeliveryDate;
        contract.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return null;
    }

    public async Task<string?> CompleteAsync(long id)
    {
        var contract = await db.Contracts.FindAsync(id);
        if (contract == null) return "合同不存在";
        if (contract.Status != "shipped") return "仅已发货状态可完成";

        contract.Status = "completed";
        contract.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return null;
    }

    public async Task<string?> TerminateAsync(long id, string? reason)
    {
        var contract = await db.Contracts.FindAsync(id);
        if (contract == null) return "合同不存在";
        if (contract.Status != "active" && contract.Status != "shipped")
            return "仅激活或已发货状态可终止";

        contract.Status = "terminated";
        if (!string.IsNullOrWhiteSpace(reason)) contract.Remark = reason;
        contract.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return null;
    }

    private async Task<string> GenerateCodeAsync()
    {
        var today = DateTime.UtcNow.ToString("yyyyMMdd");
        var prefix = $"CON-{today}-";
        var maxSeq = await db.Contracts
            .Where(x => x.ContractCode.StartsWith(prefix))
            .Select(x => x.ContractCode)
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
