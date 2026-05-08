using Microsoft.EntityFrameworkCore;
using OctopusCRM.Api.Persistence;

namespace OctopusCRM.Api.Services;

public record StatsSummaryResponse(
    int TotalCustomers,
    int ActiveCustomers,
    int OpenInquiries,
    int PendingQuotes,
    int ActiveContracts,
    int OverdueContracts,
    decimal TotalContractAmount,
    decimal CollectedAmount,
    double Dso,
    double OtdRate);

public record PipelineRow(string Stage, int Count, decimal TotalAmount);

public record OverdueContractRow(
    long ContractId,
    string ContractCode,
    string CustomerName,
    DateTime DeliveryDate,
    int OverdueDays,
    decimal TotalAmount,
    string Status);

// BI 看板专用
public record BiEfficiencyResponse(
    double InquiryToQuoteHours,
    double QuoteApprovalDays,
    double ContractCycleDays,
    double OtdRate,
    double SupplierDeliveryRate,
    double ApprovalOverdueRate,
    double Dso);

public record OtdTrendPoint(string Month, double OtdRate, int Total, int OnTime);

public record ApprovalBacklogItem(string Stage, string Label, int PendingCount, int OverdueCount);

public record ContractTimelineEvent(string Stage, string Label, DateTime? EventTime, bool IsCompleted, string? Remark);

public record ContractTimelineResponse(
    long ContractId,
    string ContractCode,
    string CustomerName,
    List<ContractTimelineEvent> Events);

public record ContractBriefRow(long ContractId, string ContractCode, string CustomerName, string Status);

public class StatsService(CrmDbContext db)
{
    public async Task<StatsSummaryResponse> GetSummaryAsync()
    {
        var today = DateTime.UtcNow.Date;

        var totalCustomers = await db.Customers.CountAsync();
        var activeCustomers = await db.Customers.CountAsync(x => x.Status == "active");
        var openInquiries = await db.Inquiries.CountAsync(x => x.Status == "open");
        var pendingQuotes = await db.Quotes.CountAsync(x => x.Status == "pending_approval");

        var activeContracts = await db.Contracts
            .CountAsync(x => x.Status == "active" || x.Status == "shipped");

        var overdueContracts = await db.Contracts
            .CountAsync(x =>
                (x.Status == "active" || x.Status == "shipped") &&
                x.DeliveryDate.HasValue &&
                x.DeliveryDate.Value < today);

        var totalContractAmount = await db.Contracts
            .Where(x => x.Status == "active" || x.Status == "shipped" || x.Status == "completed")
            .SumAsync(x => (decimal?)x.TotalAmount) ?? 0m;

        var collectedAmount = await db.Payments
            .Where(x => x.Status == "confirmed")
            .SumAsync(x => (decimal?)x.Amount) ?? 0m;

        // DSO: 平均应收天数，简化计算：(应收账款 / 年销售额) * 365
        var yearlyRevenue = await db.Contracts
            .Where(x => x.Status == "completed" &&
                        x.CreatedAt >= DateTime.UtcNow.AddYears(-1))
            .SumAsync(x => (decimal?)x.TotalAmount) ?? 0m;

        var receivable = totalContractAmount - collectedAmount;
        double dso = yearlyRevenue > 0 ? (double)(receivable / yearlyRevenue * 365) : 0;

        // OTD rate: 按时交付率（ActualDeliveryDate <= DeliveryDate 的已完成/已发货合同比例）
        var deliveredContracts = await db.Contracts
            .Where(x => (x.Status == "shipped" || x.Status == "completed") &&
                        x.ActualDeliveryDate.HasValue && x.DeliveryDate.HasValue)
            .ToListAsync();

        double otdRate = 0;
        if (deliveredContracts.Any())
        {
            var onTime = deliveredContracts.Count(x => x.ActualDeliveryDate!.Value <= x.DeliveryDate!.Value);
            otdRate = (double)onTime / deliveredContracts.Count;
        }

        return new StatsSummaryResponse(
            totalCustomers, activeCustomers, openInquiries, pendingQuotes,
            activeContracts, overdueContracts, totalContractAmount, collectedAmount,
            Math.Round(dso, 1), Math.Round(otdRate, 4));
    }

    public async Task<List<PipelineRow>> GetPipelineAsync()
    {
        var inquiryCount = await db.Inquiries.CountAsync(x => x.Status == "open");

        var quoteData = await db.Quotes
            .Where(x => x.Status == "draft" || x.Status == "pending_approval" || x.Status == "approved")
            .GroupBy(x => 1)
            .Select(g => new { Count = g.Count(), Total = g.Sum(x => x.TotalAmount) })
            .FirstOrDefaultAsync();

        var contractData = await db.Contracts
            .Where(x => x.Status == "active" || x.Status == "shipped")
            .GroupBy(x => 1)
            .Select(g => new { Count = g.Count(), Total = g.Sum(x => x.TotalAmount) })
            .FirstOrDefaultAsync();

        return new List<PipelineRow>
        {
            new("inquiry", inquiryCount, 0m),
            new("quote", quoteData?.Count ?? 0, quoteData?.Total ?? 0m),
            new("contract", contractData?.Count ?? 0, contractData?.Total ?? 0m)
        };
    }

    public async Task<List<OverdueContractRow>> GetOverdueAsync()
    {
        var today = DateTime.UtcNow.Date;

        var overdueContracts = await db.Contracts
            .Where(x =>
                (x.Status == "active" || x.Status == "shipped") &&
                x.DeliveryDate.HasValue &&
                x.DeliveryDate.Value < today)
            .ToListAsync();

        var customerIds = overdueContracts.Select(x => x.CustomerId).Distinct().ToList();
        var customers = await db.Customers
            .Where(x => customerIds.Contains(x.CustomerId))
            .ToDictionaryAsync(x => x.CustomerId, x => x.CustomerName);

        return overdueContracts.Select(x => new OverdueContractRow(
            x.ContractId,
            x.ContractCode,
            customers.TryGetValue(x.CustomerId, out var cn) ? cn : string.Empty,
            x.DeliveryDate!.Value,
            (int)(today - x.DeliveryDate.Value.Date).TotalDays,
            x.TotalAmount,
            x.Status)).ToList();
    }

    public async Task<BiEfficiencyResponse> GetEfficiencyAsync()
    {
        var now = DateTime.UtcNow;

        // 1. 询盘→报价平均时效（小时）
        var quotePairs = await db.Quotes
            .Join(db.Inquiries, q => q.InquiryId, i => i.InquiryId,
                (q, i) => new { QuoteCreatedAt = q.CreatedAt, InquiryCreatedAt = i.CreatedAt })
            .ToListAsync();
        double avgInquiryToQuoteHours = quotePairs.Any()
            ? quotePairs.Average(x => (x.QuoteCreatedAt - x.InquiryCreatedAt).TotalHours)
            : 0;

        // 2. 报价审批平均时效（天）：已审批报价 CreatedAt → UpdatedAt
        var approvedQuotes = await db.Quotes
            .Where(q => q.Status == "approved" || q.Status == "confirmed")
            .ToListAsync();
        double avgQuoteApprovalDays = approvedQuotes.Any()
            ? approvedQuotes.Average(q => (q.UpdatedAt - q.CreatedAt).TotalDays)
            : 0;

        // 3. 合同签署周期（天）：合同创建 → SignDate
        var signedContracts = await db.Contracts
            .Where(c => c.SignDate.HasValue)
            .ToListAsync();
        double avgContractCycleDays = signedContracts.Any()
            ? signedContracts.Average(c => (c.SignDate!.Value - c.CreatedAt).TotalDays)
            : 0;

        // 4. OTD（客户侧）
        var deliveredContracts = await db.Contracts
            .Where(x => (x.Status == "shipped" || x.Status == "completed") &&
                        x.ActualDeliveryDate.HasValue && x.DeliveryDate.HasValue)
            .ToListAsync();
        double otdRate = deliveredContracts.Any()
            ? (double)deliveredContracts.Count(x => x.ActualDeliveryDate!.Value <= x.DeliveryDate!.Value)
              / deliveredContracts.Count
            : 0;

        // 5. 供应侧交期达成率（MES 数据暂不跨系统，占位）
        const double supplierDeliveryRate = 0.92;

        // 6. 审批超时率（CRM 侧：在途超 2 日未处理的审批比例）
        var slaCutoff = now.AddDays(-2);
        var totalPending = await db.Quotes.CountAsync(q => q.Status == "pending_approval")
                         + await db.Contracts.CountAsync(c => c.Status == "pending_approval")
                         + await db.Payments.CountAsync(p => p.Status == "pending_approval");
        var totalOverdue = await db.Quotes.CountAsync(q => q.Status == "pending_approval" && q.UpdatedAt < slaCutoff)
                         + await db.Contracts.CountAsync(c => c.Status == "pending_approval" && c.UpdatedAt < slaCutoff)
                         + await db.Payments.CountAsync(p => p.Status == "pending_approval" && p.CreatedAt < slaCutoff);
        double approvalOverdueRate = totalPending > 0 ? (double)totalOverdue / totalPending : 0;

        // 7. DSO（应收账期）
        var totalContractAmount = await db.Contracts
            .Where(x => x.Status == "active" || x.Status == "shipped" || x.Status == "completed")
            .SumAsync(x => (decimal?)x.TotalAmount) ?? 0m;
        var collectedAmount = await db.Payments
            .Where(x => x.Status == "confirmed")
            .SumAsync(x => (decimal?)x.Amount) ?? 0m;
        var yearlyRevenue = await db.Contracts
            .Where(x => x.Status == "completed" && x.CreatedAt >= now.AddYears(-1))
            .SumAsync(x => (decimal?)x.TotalAmount) ?? 0m;
        var receivable = totalContractAmount - collectedAmount;
        double dso = yearlyRevenue > 0 ? (double)(receivable / yearlyRevenue * 365) : 0;

        return new BiEfficiencyResponse(
            Math.Round(avgInquiryToQuoteHours, 1),
            Math.Round(avgQuoteApprovalDays, 1),
            Math.Round(avgContractCycleDays, 1),
            Math.Round(otdRate, 4),
            supplierDeliveryRate,
            Math.Round(approvalOverdueRate, 4),
            Math.Round(dso, 1));
    }

    public async Task<List<OtdTrendPoint>> GetOtdTrendAsync(int months = 6)
    {
        var now = DateTime.UtcNow;
        var result = new List<OtdTrendPoint>();

        for (int i = months - 1; i >= 0; i--)
        {
            var start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-i);
            var end = start.AddMonths(1);
            var label = start.ToString("yyyy-MM");

            var delivered = await db.Contracts
                .Where(x => (x.Status == "shipped" || x.Status == "completed") &&
                            x.ActualDeliveryDate.HasValue && x.DeliveryDate.HasValue &&
                            x.ActualDeliveryDate >= start && x.ActualDeliveryDate < end)
                .ToListAsync();

            var total = delivered.Count;
            var onTime = delivered.Count(x => x.ActualDeliveryDate!.Value <= x.DeliveryDate!.Value);
            var rate = total > 0 ? (double)onTime / total : 0;

            result.Add(new OtdTrendPoint(label, Math.Round(rate, 4), total, onTime));
        }

        return result;
    }

    public async Task<List<ApprovalBacklogItem>> GetApprovalBacklogAsync()
    {
        var slaCutoff = DateTime.UtcNow.AddDays(-2);

        var pendingQuotes = await db.Quotes.CountAsync(q => q.Status == "pending_approval");
        var overdueQuotes = await db.Quotes.CountAsync(q => q.Status == "pending_approval" && q.UpdatedAt < slaCutoff);

        var pendingContracts = await db.Contracts.CountAsync(c => c.Status == "pending_approval");
        var overdueContracts = await db.Contracts.CountAsync(c => c.Status == "pending_approval" && c.UpdatedAt < slaCutoff);

        var pendingPayments = await db.Payments.CountAsync(p => p.Status == "pending_approval");
        var overduePayments = await db.Payments.CountAsync(p => p.Status == "pending_approval" && p.CreatedAt < slaCutoff);

        return
        [
            new("quote", "报价审批", pendingQuotes, overdueQuotes),
            new("contract", "合同审批", pendingContracts, overdueContracts),
            new("payment", "回款审批", pendingPayments, overduePayments)
        ];
    }

    public async Task<ContractTimelineResponse?> GetContractTimelineAsync(long contractId)
    {
        var contract = await db.Contracts
            .Include(c => c.Quote)
                .ThenInclude(q => q!.Inquiry)
            .Include(c => c.Payments)
            .FirstOrDefaultAsync(c => c.ContractId == contractId);

        if (contract == null) return null;

        var customer = await db.Customers.FindAsync(contract.CustomerId);

        var confirmedPayment = contract.Payments
            .Where(p => p.Status == "confirmed" && p.PaymentDate.HasValue)
            .OrderByDescending(p => p.PaymentDate)
            .FirstOrDefault();

        var totalConfirmed = contract.Payments.Where(p => p.Status == "confirmed").Sum(p => p.Amount);
        var paymentRemark = contract.Payments.Any()
            ? $"已收 {totalConfirmed:N0} 元 / 合同 {contract.TotalAmount:N0} 元"
            : null;

        var quoteApproved = contract.Quote?.Status is "approved" or "confirmed";
        var quoteApprovalTime = quoteApproved ? contract.Quote?.UpdatedAt : (DateTime?)null;

        var deliveryRemark = contract.ActualDeliveryDate.HasValue && contract.DeliveryDate.HasValue
            ? (contract.ActualDeliveryDate.Value <= contract.DeliveryDate.Value ? "准时交付" : "逾期交付")
            : null;

        var events = new List<ContractTimelineEvent>
        {
            new("inquiry", "客户询盘",
                contract.Quote?.Inquiry?.CreatedAt,
                contract.Quote?.Inquiry != null,
                contract.Quote?.Inquiry?.InquiryCode),

            new("quote", "提交报价",
                contract.Quote?.CreatedAt,
                contract.Quote != null,
                contract.Quote?.QuoteCode),

            new("quote_approved", "报价审批通过",
                quoteApprovalTime,
                quoteApproved,
                null),

            new("contract", "合同签署",
                contract.SignDate,
                contract.SignDate.HasValue,
                contract.ContractCode),

            new("shipped", "发货出库",
                contract.ActualDeliveryDate,
                contract.ActualDeliveryDate.HasValue,
                deliveryRemark),

            new("payment", "收款确认",
                confirmedPayment?.PaymentDate,
                confirmedPayment != null,
                paymentRemark)
        };

        return new ContractTimelineResponse(
            contractId,
            contract.ContractCode,
            customer?.CustomerName ?? string.Empty,
            events);
    }

    public async Task<List<ContractBriefRow>> GetContractBriefListAsync()
    {
        var contracts = await db.Contracts
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        var customerIds = contracts.Select(c => c.CustomerId).Distinct().ToList();
        var customers = await db.Customers
            .Where(x => customerIds.Contains(x.CustomerId))
            .ToDictionaryAsync(x => x.CustomerId, x => x.CustomerName);

        return contracts.Select(c => new ContractBriefRow(
            c.ContractId,
            c.ContractCode,
            customers.TryGetValue(c.CustomerId, out var cn) ? cn : string.Empty,
            c.Status)).ToList();
    }
}
