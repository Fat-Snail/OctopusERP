using Microsoft.EntityFrameworkCore;
using OctopusMES.Api.Persistence;

namespace OctopusMES.Api.Services;

public class PurchaseOrderService(MesDbContext db, IOaFlowClient oaFlow)
{
    public async Task<object> GetListAsync(string? status, long? supplierId, int pageNum, int pageSize)
    {
        var q = db.PurchaseOrders.Include(x => x.Supplier).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(x => x.Status == status);
        if (supplierId.HasValue) q = q.Where(x => x.SupplierId == supplierId.Value);

        var total = await q.CountAsync();
        var list = await q.OrderByDescending(x => x.CreatedAt).Skip((pageNum - 1) * pageSize).Take(pageSize).ToListAsync();
        return new { rows = list, total };
    }

    public async Task<MesPurchaseOrder?> GetByIdAsync(long id) =>
        await db.PurchaseOrders.Include(x => x.Items).Include(x => x.Supplier).FirstOrDefaultAsync(x => x.PurchaseId == id);

    public async Task<MesPurchaseOrder> CreateAsync(MesPurchaseOrder req, long userId)
    {
        req.PurchaseCode = $"PO-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}";
        req.Status = "draft";
        req.CreatedBy = userId;
        req.CreatedAt = DateTime.UtcNow;
        req.UpdatedAt = DateTime.UtcNow;
        req.TotalAmount = req.Items.Sum(i => i.Amount);
        db.PurchaseOrders.Add(req);
        await db.SaveChangesAsync();
        return req;
    }

    public async Task<MesPurchaseOrder?> SubmitAsync(long id)
    {
        var po = await db.PurchaseOrders
            .Include(x => x.Items).Include(x => x.Supplier)
            .FirstOrDefaultAsync(x => x.PurchaseId == id);
        if (po is null || po.Status != "draft") return null;
        po.Status = "submitted";
        po.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // 接入审批中心：发起采购流程并回填流程实例 id（未配置 OA 时为空操作）
        var instanceId = await oaFlow.SubmitPurchaseAsync(po);
        if (instanceId.HasValue)
        {
            po.OaApprovalId = instanceId.Value;
            await db.SaveChangesAsync();
        }
        return po;
    }

    public async Task<MesPurchaseOrder?> ApproveAsync(long id)
    {
        var po = await db.PurchaseOrders.FindAsync(id);
        if (po is null || po.Status != "submitted") return null;
        po.Status = "approved";
        po.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return po;
    }

    public async Task<MesPurchaseOrder?> RejectAsync(long id)
    {
        var po = await db.PurchaseOrders.FindAsync(id);
        if (po is null || po.Status != "submitted") return null;
        po.Status = "rejected";
        po.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return po;
    }
}
