using Microsoft.EntityFrameworkCore;
using OctopusMES.Api.Persistence;

namespace OctopusMES.Api.Services;

public class SupplierService(MesDbContext db)
{
    public async Task<object> GetListAsync(string? keyword, string? status, int pageNum, int pageSize)
    {
        var q = db.Suppliers.AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
            q = q.Where(x => x.SupplierName.Contains(keyword) || x.SupplierCode.Contains(keyword));
        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(x => x.Status == status);

        var total = await q.CountAsync();
        var list = await q.OrderBy(x => x.SupplierCode).Skip((pageNum - 1) * pageSize).Take(pageSize).ToListAsync();
        return new { rows = list, total };
    }

    public async Task<MesSupplier?> GetByIdAsync(long id) =>
        await db.Suppliers.FindAsync(id);

    public async Task<MesSupplier> CreateAsync(MesSupplier req)
    {
        req.SupplierCode = $"SUP-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
        req.CreatedAt = DateTime.UtcNow;
        db.Suppliers.Add(req);
        await db.SaveChangesAsync();
        return req;
    }

    public async Task<MesSupplier?> UpdateAsync(MesSupplier req)
    {
        var s = await db.Suppliers.FindAsync(req.SupplierId);
        if (s is null) return null;
        s.SupplierName = req.SupplierName;
        s.ContactName = req.ContactName;
        s.Phone = req.Phone;
        s.Email = req.Email;
        s.Address = req.Address;
        s.BankAccount = req.BankAccount;
        s.TaxNumber = req.TaxNumber;
        s.Status = req.Status;
        s.Level = req.Level;
        s.Remark = req.Remark;
        await db.SaveChangesAsync();
        return s;
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var s = await db.Suppliers.FindAsync(id);
        if (s is null) return false;
        db.Suppliers.Remove(s);
        await db.SaveChangesAsync();
        return true;
    }
}
