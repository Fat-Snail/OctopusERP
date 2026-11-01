using Microsoft.EntityFrameworkCore;
using OctopusFIN.Api.Persistence;

namespace OctopusFIN.Api.Services;

public class AccountService(FinDbContext db)
{
    public async Task<List<FinAccount>> GetListAsync(string? type, string? keyword)
    {
        var q = db.Accounts.AsQueryable();
        if (!string.IsNullOrEmpty(type)) q = q.Where(x => x.AccountType == type);
        if (!string.IsNullOrEmpty(keyword))
            q = q.Where(x => x.AccountCode.Contains(keyword) || x.AccountName.Contains(keyword));
        return await q.OrderBy(x => x.AccountCode).ToListAsync();
    }

    public async Task<FinAccount?> GetByIdAsync(long id) =>
        await db.Accounts.FirstOrDefaultAsync(x => x.AccountId == id);

    public async Task<FinAccount?> GetByCodeAsync(string code) =>
        await db.Accounts.FirstOrDefaultAsync(x => x.AccountCode == code);

    public async Task<(FinAccount? account, string? error)> CreateAsync(FinAccount input)
    {
        if (string.IsNullOrWhiteSpace(input.AccountCode)) return (null, "科目编码不能为空");
        if (string.IsNullOrWhiteSpace(input.AccountName)) return (null, "科目名称不能为空");
        if (await db.Accounts.AnyAsync(x => x.AccountCode == input.AccountCode))
            return (null, "科目编码已存在");

        input.CreatedAt = DateTime.UtcNow;
        if (input.ParentId.HasValue)
        {
            var parent = await db.Accounts.FirstOrDefaultAsync(x => x.AccountId == input.ParentId.Value);
            if (parent != null)
            {
                input.Level = parent.Level + 1;
                if (parent.IsLeaf) { parent.IsLeaf = false; }
            }
        }
        db.Accounts.Add(input);
        await db.SaveChangesAsync();
        return (input, null);
    }

    public async Task<(FinAccount? account, string? error)> UpdateAsync(long id, string? name, string? remark, string? status)
    {
        var account = await db.Accounts.FirstOrDefaultAsync(x => x.AccountId == id);
        if (account == null) return (null, "科目不存在");
        if (!string.IsNullOrEmpty(name)) account.AccountName = name;
        if (remark != null) account.Remark = remark;
        if (!string.IsNullOrEmpty(status)) account.Status = status;
        await db.SaveChangesAsync();
        return (account, null);
    }
}
