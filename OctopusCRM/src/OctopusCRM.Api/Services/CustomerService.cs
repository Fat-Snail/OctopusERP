using Microsoft.EntityFrameworkCore;
using OctopusCRM.Api.Persistence;

namespace OctopusCRM.Api.Services;

public record CustomerListRow(
    long CustomerId,
    string CustomerCode,
    string CustomerName,
    string? IndustryType,
    string Level,
    string Status,
    int ContactCount,
    int InquiryCount,
    DateTime CreatedAt);

public class CustomerService(CrmDbContext db)
{
    public async Task<(List<CustomerListRow> Rows, int Total)> GetListAsync(
        string? keyword, string? level, string? status, int page = 1, int size = 20)
    {
        var query = db.Customers.AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(x => x.CustomerName.Contains(keyword) || x.CustomerCode.Contains(keyword));
        if (!string.IsNullOrWhiteSpace(level))
            query = query.Where(x => x.Level == level);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status == status);

        var total = await query.CountAsync();

        var customers = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(x => new
            {
                x.CustomerId, x.CustomerCode, x.CustomerName, x.IndustryType,
                x.Level, x.Status, x.CreatedAt,
                ContactCount = x.Contacts.Count,
                InquiryCount = x.Inquiries.Count
            })
            .ToListAsync();

        var rows = customers.Select(x => new CustomerListRow(
            x.CustomerId, x.CustomerCode, x.CustomerName, x.IndustryType,
            x.Level, x.Status, x.ContactCount, x.InquiryCount, x.CreatedAt)).ToList();

        return (rows, total);
    }

    public async Task<CrmCustomer?> GetByIdAsync(long id)
    {
        return await db.Customers
            .Include(x => x.Contacts)
            .Include(x => x.Inquiries)
            .FirstOrDefaultAsync(x => x.CustomerId == id);
    }

    public async Task<CrmCustomer> CreateAsync(
        string name, string? industryType, string level, string status,
        string? address, string? website, string? remark, long createdBy)
    {
        var code = await GenerateCodeAsync();
        var now = DateTime.UtcNow;
        var customer = new CrmCustomer
        {
            CustomerCode = code,
            CustomerName = name,
            IndustryType = industryType,
            Level = level,
            Status = status,
            Address = address,
            Website = website,
            Remark = remark,
            CreatedBy = createdBy,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        return customer;
    }

    public async Task<string?> UpdateAsync(
        long id, string? name, string? industryType, string? level, string? status,
        string? address, string? website, string? remark)
    {
        var customer = await db.Customers.FindAsync(id);
        if (customer == null) return "客户不存在";

        if (!string.IsNullOrWhiteSpace(name)) customer.CustomerName = name;
        if (industryType != null) customer.IndustryType = industryType;
        if (!string.IsNullOrWhiteSpace(level)) customer.Level = level;
        if (!string.IsNullOrWhiteSpace(status)) customer.Status = status;
        if (address != null) customer.Address = address;
        if (website != null) customer.Website = website;
        if (remark != null) customer.Remark = remark;
        customer.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return null;
    }

    public async Task<string?> DeleteAsync(long id)
    {
        var customer = await db.Customers
            .Include(x => x.Inquiries)
            .FirstOrDefaultAsync(x => x.CustomerId == id);
        if (customer == null) return "客户不存在";
        if (customer.Inquiries.Any()) return "该客户下有询盘，无法删除";

        var hasContract = await db.Contracts.AnyAsync(x => x.CustomerId == id);
        if (hasContract) return "该客户下有合同，无法删除";

        db.Customers.Remove(customer);
        await db.SaveChangesAsync();
        return null;
    }

    public async Task<CrmContact> AddContactAsync(
        long customerId, string name, string? title, string? phone, string? email,
        bool isPrimary, string? remark)
    {
        var contact = new CrmContact
        {
            CustomerId = customerId,
            Name = name,
            Title = title,
            Phone = phone,
            Email = email,
            IsPrimary = isPrimary,
            Remark = remark,
            CreatedAt = DateTime.UtcNow
        };
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();
        return contact;
    }

    public async Task<string?> UpdateContactAsync(
        long contactId, string? name, string? title, string? phone, string? email,
        bool? isPrimary, string? remark)
    {
        var contact = await db.Contacts.FindAsync(contactId);
        if (contact == null) return "联系人不存在";

        if (!string.IsNullOrWhiteSpace(name)) contact.Name = name;
        if (title != null) contact.Title = title;
        if (phone != null) contact.Phone = phone;
        if (email != null) contact.Email = email;
        if (isPrimary.HasValue) contact.IsPrimary = isPrimary.Value;
        if (remark != null) contact.Remark = remark;

        await db.SaveChangesAsync();
        return null;
    }

    public async Task<string?> DeleteContactAsync(long contactId)
    {
        var contact = await db.Contacts.FindAsync(contactId);
        if (contact == null) return "联系人不存在";

        db.Contacts.Remove(contact);
        await db.SaveChangesAsync();
        return null;
    }

    private async Task<string> GenerateCodeAsync()
    {
        var today = DateTime.UtcNow.ToString("yyyyMMdd");
        var prefix = $"CUS-{today}-";
        var maxSeq = await db.Customers
            .Where(x => x.CustomerCode.StartsWith(prefix))
            .Select(x => x.CustomerCode)
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
