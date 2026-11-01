using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OctopusMES.Api.Persistence;
using OctopusMES.Api.Services.PlmSync;

namespace OctopusMES.Api.Services;

public sealed record PlmCatalogItem(long ProductId, long? ManufacturingVersionId, string ProductCode, string ProductName, string? Version, string? BomVersion, string? RoutingVersion, string Status, int BomItemCount = 0, int OperationCount = 0);
public sealed record PlmCatalogResult(bool Available, string SourceSystem, string Message, IReadOnlyList<PlmCatalogItem> Products);
public sealed record PlmCatalogPage(bool Available, string Message, IReadOnlyList<PlmCatalogItem> Rows, int Total, int Page, int PageSize, bool HasMore);
public sealed record PlmReconcileRow(long ProductId, string ProductCode, string ProductName, long? ManufacturingVersionId, string? Version, string? BomVersion, string? RoutingVersion, string SyncStatus, bool IsSame, string? Difference);
public sealed record PlmReconcileResult(bool Available, string Message, int PlmTotal, int ReleasedTotal, int LocalRecordTotal, int SameTotal, int DifferentTotal, int SyncedTotal, int UnreleasedTotal, IReadOnlyList<PlmReconcileRow> Rows);
public sealed record PlmSyncRecordRow(long SyncRecordId, long PlmProductId, long? PlmManufacturingVersionId, string PlmVersion, string? ProductCode, string? ProductName, string Status, string? PayloadHash, int RetryCount, DateTime? NextRetryAt, DateTime? SyncedAt, string? ErrorMessage, bool RequiresManualAction, long? ManufacturingSnapshotId);
public sealed record PlmSyncRecordPage(IReadOnlyList<PlmSyncRecordRow> Rows, int Total, int Page, int PageSize, bool HasMore);
public sealed record PlmSyncAttemptRow(int AttemptNumber, string Status, string? ErrorCode, string? ErrorMessage, string? ValidationIssuesJson, bool Retryable, DateTime StartedAt, DateTime FinishedAt);
public sealed record PlmSnapshotDetail(long ManufacturingSnapshotId, string ProductCode, string ProductName, string ProductVersion, string BomVersion, string RoutingVersion, string PayloadHash, string Status, IReadOnlyList<MesSnapshotBomItem> BomItems, IReadOnlyList<MesSnapshotOperation> Operations);
public sealed record PlmRelatedOrder(long ProductionOrderId, string ProductionOrderCode, string Status, decimal PlannedQty, DateTime? DueDate);
public sealed record PlmSyncRecordDetail(PlmSyncRecordRow Record, IReadOnlyList<PlmSyncAttemptRow> Attempts, PlmSnapshotDetail? Snapshot, IReadOnlyList<PlmRelatedOrder> RelatedOrders);

public sealed class PlmProductSyncService(IHttpClientFactory httpFactory, IConfiguration config, MesDbContext db, ILogger<PlmProductSyncService> logger, PlmManufacturingSyncService manufacturingSync)
{
    public async Task<PlmCatalogResult> GetCatalogAsync(string? keyword, CancellationToken ct)
    {
        var baseUrl = config["Plm:BaseUrl"] ?? "http://localhost:5003";
        try
        {
            var client = httpFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            var response = await client.GetAsync($"{baseUrl}/api/product/manufacturing?keyword={Uri.EscapeDataString(keyword ?? string.Empty)}", ct);
            if (!response.IsSuccessStatusCode)
                return new(false, "PLM", $"PLM 产品目录请求失败（HTTP {(int)response.StatusCode}）", []);
            var wrapper = await response.Content.ReadFromJsonAsync<ApiWrapper<List<PlmProductRow>>>(cancellationToken: ct);
            if (wrapper?.Code != 200)
                return new(false, "PLM", wrapper?.Msg ?? "PLM 产品目录返回异常", []);
            var products = (wrapper.Data ?? []).Select(x => new PlmCatalogItem(x.ProductId, x.ManufacturingVersionId, x.ProductCode, x.ProductName, x.Version, x.BomVersion, x.RoutingVersion, x.Status)).ToList();
            return new(true, "PLM", products.Count == 0 ? "PLM 暂无已发布制造产品" : $"已从 PLM 加载 {products.Count} 个产品", products);
        }
        catch (Exception ex) { logger.LogWarning(ex, "PLM 产品目录不可达"); return new(false, "PLM", "PLM 服务不可用，已禁止使用本地或演示产品创建订单", []); }
    }

    public async Task<PlmCatalogPage> SearchCatalogAsync(string? keyword, int page, int pageSize, CancellationToken ct)
    {
        var baseUrl = config["Plm:BaseUrl"] ?? "http://localhost:5003";
        try
        {
            var client = httpFactory.CreateClient(); client.Timeout = TimeSpan.FromSeconds(5);
            var url = $"{baseUrl}/api/product/manufacturing/search?keyword={Uri.EscapeDataString(keyword ?? string.Empty)}&page={Math.Max(1, page)}&pageSize={Math.Clamp(pageSize, 1, 50)}";
            var wrapper = await client.GetFromJsonAsync<ApiWrapper<CatalogPagePayload>>(url, ct);
            if (wrapper?.Code != 200 || wrapper.Data is null) return new(false, wrapper?.Msg ?? "PLM 商品搜索失败", [], 0, page, pageSize, false);
            var rows = wrapper.Data.Rows.Select(x => new PlmCatalogItem(x.ProductId, x.Manufacturing?.ManufacturingVersionId, x.ProductCode, x.ProductName, x.Manufacturing?.Version, x.Manufacturing?.BomVersion, x.Manufacturing?.RoutingVersion, x.Manufacturing?.Status ?? "unreleased", x.Manufacturing?.BomItemCount ?? 0, x.Manufacturing?.OperationCount ?? 0)).ToList();
            return new(true, "ok", rows, wrapper.Data.Total, wrapper.Data.Page, wrapper.Data.PageSize, wrapper.Data.HasMore);
        }
        catch (Exception ex) { logger.LogWarning(ex, "PLM 商品搜索失败"); return new(false, "PLM 服务不可用", [], 0, page, pageSize, false); }
    }

    public async Task<MesProduct> SyncAsync(long productId, CancellationToken ct)
    {
        var catalog = await GetCatalogAsync(productId.ToString(), ct);
        if (!catalog.Available) { await RecordFailureAsync(productId, "unknown", catalog.Message, ct); throw new InvalidOperationException(catalog.Message); }
        var source = catalog.Products.FirstOrDefault(x => x.ProductId == productId);
        if (source is null) { await RecordFailureAsync(productId, "unknown", "PLM 产品不存在或未激活", ct); throw new InvalidOperationException("PLM 产品不存在或未激活"); }
        var sourceVersion = source.Version ?? throw new InvalidOperationException("PLM 制造版本号缺失");
        var local = await db.Products.FirstOrDefaultAsync(x => x.ProductCode == source.ProductCode && x.Version == sourceVersion, ct);
        if (local is null) { local = new MesProduct { ProductCode = source.ProductCode, ProductName = source.ProductName, Version = sourceVersion, Status = "active", CreatedAt = DateTime.UtcNow }; db.Products.Add(local); } else { local.ProductName = source.ProductName; local.Status = "active"; }
        await db.SaveChangesAsync(ct);
        var record = await db.PlmProductSyncRecords.FirstOrDefaultAsync(x => x.PlmProductId == productId && x.PlmVersion == sourceVersion, ct);
        record ??= new MesPlmProductSyncRecord { PlmProductId = productId, PlmVersion = sourceVersion, CreatedAt = DateTime.UtcNow };
        if (record.SyncRecordId == 0) db.PlmProductSyncRecords.Add(record);
        record.MesProductId = local.ProductId; record.SourceSystem = "PLM"; record.Status = "synced"; record.ErrorMessage = null;
        record.PayloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{source.ProductId}|{source.ProductCode}|{source.ProductName}|{source.Version}"))).ToLowerInvariant();
        record.SyncedAt = DateTime.UtcNow; record.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct); return local;
    }

    public Task<List<MesPlmProductSyncRecord>> ListSyncRecordsAsync(CancellationToken ct) =>
        db.PlmProductSyncRecords.AsNoTracking().OrderByDescending(x => x.UpdatedAt).Take(500).ToListAsync(ct);

    public async Task<PlmSyncRecordPage> ListSyncRecordsAsync(string? status, string? keyword, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 50);
        var query = db.PlmProductSyncRecords.AsNoTracking().Include(x => x.ManufacturingSnapshot).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim();
            query = query.Where(x => x.ManufacturingSnapshot != null &&
                (x.ManufacturingSnapshot.ProductCode.Contains(term) || x.ManufacturingSnapshot.ProductName.Contains(term)));
        }
        var total = await query.CountAsync(ct);
        var records = await query.OrderByDescending(x => x.UpdatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var rows = records.Select(ToRow).ToList();
        return new(rows, total, page, pageSize, page * pageSize < total);
    }

    public async Task<PlmSyncRecordDetail> GetSyncRecordDetailAsync(long syncRecordId, CancellationToken ct)
    {
        var record = await db.PlmProductSyncRecords.AsNoTracking().Include(x => x.ManufacturingSnapshot)!.ThenInclude(x => x!.BomItems)
            .Include(x => x.ManufacturingSnapshot)!.ThenInclude(x => x!.Operations)
            .SingleOrDefaultAsync(x => x.SyncRecordId == syncRecordId, ct) ?? throw new InvalidOperationException("同步记录不存在");
        var attempts = await db.PlmSyncAttempts.AsNoTracking().Where(x => x.SyncRecordId == syncRecordId).OrderByDescending(x => x.AttemptNumber)
            .Select(x => new PlmSyncAttemptRow(x.AttemptNumber, x.Status, x.ErrorCode, x.ErrorMessage, x.ValidationIssuesJson, x.Retryable, x.StartedAt, x.FinishedAt)).ToListAsync(ct);
        var orders = record.ManufacturingSnapshotId.HasValue
            ? await db.ProductionOrders.AsNoTracking().Where(x => x.ManufacturingSnapshotId == record.ManufacturingSnapshotId)
                .Select(x => new PlmRelatedOrder(x.ProductionOrderId, x.ProductionOrderCode, x.Status, x.PlannedQty, x.DueDate)).ToListAsync(ct)
            : [];
        var snapshot = record.ManufacturingSnapshot is null ? null : new PlmSnapshotDetail(record.ManufacturingSnapshot.ManufacturingSnapshotId,
            record.ManufacturingSnapshot.ProductCode, record.ManufacturingSnapshot.ProductName, record.ManufacturingSnapshot.ProductVersion,
            record.ManufacturingSnapshot.BomVersion, record.ManufacturingSnapshot.RoutingVersion, record.ManufacturingSnapshot.PayloadHash,
            record.ManufacturingSnapshot.Status, record.ManufacturingSnapshot.BomItems, record.ManufacturingSnapshot.Operations);
        return new(ToRow(record), attempts, snapshot, orders);
    }

    public async Task<MesPlmProductSyncRecord> RetryRecordAsync(long syncRecordId, CancellationToken ct)
    {
        var record = await db.PlmProductSyncRecords.SingleOrDefaultAsync(x => x.SyncRecordId == syncRecordId, ct)
            ?? throw new InvalidOperationException("同步记录不存在");
        if (!record.PlmManufacturingVersionId.HasValue) throw new InvalidOperationException("同步记录缺少 PLM 制造版本 ID");
        record.Status = "pending"; record.RequiresManualAction = false; record.NextRetryAt = null; record.LeaseUntil = null; record.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await manufacturingSync.SyncAsync(record.SyncRecordId, ct);
        return await db.PlmProductSyncRecords.AsNoTracking().SingleAsync(x => x.SyncRecordId == syncRecordId, ct);
    }

    private static PlmSyncRecordRow ToRow(MesPlmProductSyncRecord x) => new(x.SyncRecordId, x.PlmProductId,
        x.PlmManufacturingVersionId, x.PlmVersion, x.ManufacturingSnapshot?.ProductCode, x.ManufacturingSnapshot?.ProductName,
        x.Status, x.PayloadHash, x.RetryCount, x.NextRetryAt, x.SyncedAt, x.ErrorMessage, x.RequiresManualAction, x.ManufacturingSnapshotId);

    public async Task<PlmReconcileResult> ReconcileAsync(bool initialize, CancellationToken ct)
    {
        var baseUrl = config["Plm:BaseUrl"] ?? "http://localhost:5003";
        try
        {
            var client = httpFactory.CreateClient(); client.Timeout = TimeSpan.FromSeconds(10);
            var wrapper = await client.GetFromJsonAsync<ApiWrapper<List<PlmBrowseRow>>>($"{baseUrl}/api/product/mes-catalog", ct);
            if (wrapper?.Code != 200) return new(false, wrapper?.Msg ?? "PLM 商品目录读取失败", 0, 0, 0, 0, 0, 0, 0, []);
            var source = wrapper.Data ?? [];
            var records = await db.PlmProductSyncRecords.AsNoTracking().ToListAsync(ct);
            if (initialize)
            {
                foreach (var item in source.Where(x => x.Manufacturing is not null))
                {
                    var m = item.Manufacturing!;
                    var existing = await db.PlmProductSyncRecords.FirstOrDefaultAsync(x => x.PlmProductId == item.ProductId && x.PlmVersion == m.Version, ct);
                    if (existing is null)
                        db.PlmProductSyncRecords.Add(new MesPlmProductSyncRecord { PlmProductId = item.ProductId, PlmManufacturingVersionId = m.ManufacturingVersionId, PlmVersion = m.Version, SourceSystem = "PLM", Status = "pending", PayloadHash = m.PayloadHash, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
                    else if (PlmReconciliationRules.NeedsInitialization(existing))
                    {
                        existing.PlmManufacturingVersionId = m.ManufacturingVersionId;
                        existing.PayloadHash = m.PayloadHash;
                        existing.Status = "pending";
                        existing.ErrorMessage = null;
                        existing.RequiresManualAction = false;
                        existing.NextRetryAt = null;
                        existing.LeaseUntil = null;
                        existing.UpdatedAt = DateTime.UtcNow;
                    }
                }
                await db.SaveChangesAsync(ct); records = await db.PlmProductSyncRecords.AsNoTracking().ToListAsync(ct);
            }
            var rows = source.Select(item =>
            {
                var m = item.Manufacturing; var record = m is null ? null : records.FirstOrDefault(x => x.PlmProductId == item.ProductId && x.PlmVersion == m.Version);
                var same = m is not null && PlmReconciliationRules.IsSame(record, m.ManufacturingVersionId, m.PayloadHash);
                var difference = m is null ? "PLM 尚未发布制造版本" : record is null ? "MES 尚未初始化同步记录" : !record.ManufacturingSnapshotId.HasValue ? "MES 尚未生成不可变制造快照" : record.Status != "synced" ? $"MES 状态为 {record.Status}" : !string.Equals(record.PayloadHash, m.PayloadHash, StringComparison.OrdinalIgnoreCase) ? "内容哈希不一致" : null;
                return new PlmReconcileRow(item.ProductId, item.ProductCode, item.ProductName, m?.ManufacturingVersionId, m?.Version, m?.BomVersion, m?.RoutingVersion, record?.Status ?? (m is null ? "unreleased" : "uninitialized"), same, difference);
            }).ToList();
            return new(true, "PLM 对账完成", source.Count, source.Count(x => x.Manufacturing is not null), records.Count, rows.Count(x => x.IsSame), rows.Count(x => x.ManufacturingVersionId.HasValue && !x.IsSame), records.Count(x => x.Status == "synced"), rows.Count(x => !x.ManufacturingVersionId.HasValue), rows);
        }
        catch (Exception ex) { logger.LogWarning(ex, "PLM 对账失败"); return new(false, "PLM 服务不可用，对账未执行", 0, 0, 0, 0, 0, 0, 0, []); }
    }

    public async Task<MesPlmProductSyncRecord> RetryAsync(long productId, CancellationToken ct)
    {
        var record = await db.PlmProductSyncRecords.AsNoTracking().Where(x => x.PlmProductId == productId).OrderByDescending(x => x.UpdatedAt).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("同步记录不存在");
        if (!record.PlmManufacturingVersionId.HasValue) throw new InvalidOperationException("同步记录缺少 PLM 制造版本 ID");
        return await SyncManufacturingVersionAsync(productId, record.PlmManufacturingVersionId.Value, ct);
    }

    public async Task<MesPlmProductSyncRecord> SyncManufacturingVersionAsync(long productId, long manufacturingVersionId, CancellationToken ct)
    {
        var record = await db.PlmProductSyncRecords
            .FirstOrDefaultAsync(x => x.PlmProductId == productId && x.PlmManufacturingVersionId == manufacturingVersionId, ct);
        if (record is null)
        {
            record = new MesPlmProductSyncRecord
            {
                PlmProductId = productId,
                PlmManufacturingVersionId = manufacturingVersionId,
                PlmVersion = $"manufacturing-{manufacturingVersionId}",
                SourceSystem = "PLM",
                Status = "pending",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            db.PlmProductSyncRecords.Add(record);
            await db.SaveChangesAsync(ct);
        }
        await manufacturingSync.SyncAsync(record.SyncRecordId, ct);
        return await db.PlmProductSyncRecords.AsNoTracking().SingleAsync(x => x.SyncRecordId == record.SyncRecordId, ct);
    }

    private async Task RecordFailureAsync(long productId, string version, string message, CancellationToken ct)
    {
        var record = await db.PlmProductSyncRecords.FirstOrDefaultAsync(x => x.PlmProductId == productId && x.PlmVersion == version, ct);
        record ??= new MesPlmProductSyncRecord { PlmProductId = productId, PlmVersion = version, CreatedAt = DateTime.UtcNow };
        if (record.SyncRecordId == 0) db.PlmProductSyncRecords.Add(record);
        record.Status = "failed"; record.SourceSystem = "PLM"; record.ErrorMessage = message; record.RetryCount++; record.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private sealed record PlmProductRow(long ProductId, long ManufacturingVersionId, string ProductCode, string ProductName, string Version, string BomVersion, string RoutingVersion, string Status, string? PayloadHash);
    private sealed record PlmBrowseRow(long ProductId, string ProductCode, string ProductName, string ProductStatus, BrowseManufacturing? Manufacturing);
    private sealed record BrowseManufacturing(long ManufacturingVersionId, string Version, string BomVersion, string RoutingVersion, string? PayloadHash, DateTime? ReleasedAt);
    private sealed record CatalogPagePayload(List<PlmSearchRow> Rows, int Total, int Page, int PageSize, bool HasMore);
    private sealed record PlmSearchRow(long ProductId, string ProductCode, string ProductName, SearchManufacturing? Manufacturing);
    private sealed record SearchManufacturing(long ManufacturingVersionId, string Version, string Status, string BomVersion, string RoutingVersion, string? PayloadHash, int BomItemCount, int OperationCount);
    private sealed record ApiWrapper<T>(int Code, string? Msg, T? Data);
}
