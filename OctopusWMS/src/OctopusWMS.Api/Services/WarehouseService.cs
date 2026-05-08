using Microsoft.EntityFrameworkCore;
using OctopusWMS.Api.Persistence;

namespace OctopusWMS.Api.Services;

public class WarehouseService(WmsDbContext db)
{
    public async Task<object> GetListAsync(string? keyword, string? status)
    {
        var q = db.Warehouses.AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
            q = q.Where(x => x.WarehouseName.Contains(keyword) || x.WarehouseCode.Contains(keyword));
        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(x => x.Status == status);

        var list = await q.OrderBy(x => x.WarehouseCode).ToListAsync();
        return new { rows = list, total = list.Count };
    }

    public async Task<WmsWarehouse?> GetByIdAsync(long id) =>
        await db.Warehouses.Include(x => x.Locations).FirstOrDefaultAsync(x => x.WarehouseId == id);

    public async Task<WmsWarehouse> CreateAsync(WmsWarehouse w)
    {
        w.WarehouseCode = $"WH-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
        w.CreatedAt = DateTime.UtcNow;
        db.Warehouses.Add(w);
        await db.SaveChangesAsync();
        return w;
    }

    public async Task<WmsWarehouse?> UpdateAsync(WmsWarehouse req)
    {
        var w = await db.Warehouses.FindAsync(req.WarehouseId);
        if (w is null) return null;
        w.WarehouseName = req.WarehouseName;
        w.Address = req.Address;
        w.Manager = req.Manager;
        w.Phone = req.Phone;
        w.Status = req.Status;
        w.Remark = req.Remark;
        await db.SaveChangesAsync();
        return w;
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var w = await db.Warehouses.FindAsync(id);
        if (w is null) return false;
        db.Warehouses.Remove(w);
        await db.SaveChangesAsync();
        return true;
    }
}
