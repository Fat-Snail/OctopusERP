using Microsoft.EntityFrameworkCore;

namespace OctopusCRM.Api.Persistence;

public class CrmDbContext : DbContext
{
    public CrmDbContext(DbContextOptions<CrmDbContext> options) : base(options) { }

    public DbSet<CrmSyncUser> SyncUsers => Set<CrmSyncUser>();
    public DbSet<CrmCustomer> Customers => Set<CrmCustomer>();
    public DbSet<CrmContact> Contacts => Set<CrmContact>();
    public DbSet<CrmInquiry> Inquiries => Set<CrmInquiry>();
    public DbSet<CrmInquiryItem> InquiryItems => Set<CrmInquiryItem>();
    public DbSet<CrmQuote> Quotes => Set<CrmQuote>();
    public DbSet<CrmQuoteItem> QuoteItems => Set<CrmQuoteItem>();
    public DbSet<CrmContract> Contracts => Set<CrmContract>();
    public DbSet<CrmContractItem> ContractItems => Set<CrmContractItem>();
    public DbSet<CrmPayment> Payments => Set<CrmPayment>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<CrmSyncUser>().ToTable("crm_sync_user");
        b.Entity<CrmCustomer>().ToTable("crm_customer");
        b.Entity<CrmContact>().ToTable("crm_contact");
        b.Entity<CrmInquiry>().ToTable("crm_inquiry");
        b.Entity<CrmInquiryItem>().ToTable("crm_inquiry_item");
        b.Entity<CrmQuote>().ToTable("crm_quote");
        b.Entity<CrmQuoteItem>().ToTable("crm_quote_item");
        b.Entity<CrmContract>().ToTable("crm_contract");
        b.Entity<CrmContractItem>().ToTable("crm_contract_item");
        b.Entity<CrmPayment>().ToTable("crm_payment");

        // 显式主键 + 自增
        b.Entity<CrmSyncUser>().HasKey(x => x.Id);
        b.Entity<CrmSyncUser>().Property(x => x.Id).ValueGeneratedOnAdd();
        b.Entity<CrmCustomer>().HasKey(x => x.CustomerId);
        b.Entity<CrmCustomer>().Property(x => x.CustomerId).ValueGeneratedOnAdd();
        b.Entity<CrmContact>().HasKey(x => x.ContactId);
        b.Entity<CrmContact>().Property(x => x.ContactId).ValueGeneratedOnAdd();
        b.Entity<CrmInquiry>().HasKey(x => x.InquiryId);
        b.Entity<CrmInquiry>().Property(x => x.InquiryId).ValueGeneratedOnAdd();
        b.Entity<CrmInquiryItem>().HasKey(x => x.ItemId);
        b.Entity<CrmInquiryItem>().Property(x => x.ItemId).ValueGeneratedOnAdd();
        b.Entity<CrmQuote>().HasKey(x => x.QuoteId);
        b.Entity<CrmQuote>().Property(x => x.QuoteId).ValueGeneratedOnAdd();
        b.Entity<CrmQuoteItem>().HasKey(x => x.ItemId);
        b.Entity<CrmQuoteItem>().Property(x => x.ItemId).ValueGeneratedOnAdd();
        b.Entity<CrmContract>().HasKey(x => x.ContractId);
        b.Entity<CrmContract>().Property(x => x.ContractId).ValueGeneratedOnAdd();
        b.Entity<CrmContractItem>().HasKey(x => x.ItemId);
        b.Entity<CrmContractItem>().Property(x => x.ItemId).ValueGeneratedOnAdd();
        b.Entity<CrmPayment>().HasKey(x => x.PaymentId);
        b.Entity<CrmPayment>().Property(x => x.PaymentId).ValueGeneratedOnAdd();

        // Decimal 精度
        b.Entity<CrmInquiryItem>().Property(x => x.Quantity).HasColumnType("decimal(18,4)");
        b.Entity<CrmQuoteItem>().Property(x => x.Quantity).HasColumnType("decimal(18,4)");
        b.Entity<CrmQuoteItem>().Property(x => x.UnitPrice).HasColumnType("decimal(18,4)");
        b.Entity<CrmQuoteItem>().Property(x => x.Amount).HasColumnType("decimal(18,4)");
        b.Entity<CrmQuote>().Property(x => x.TotalAmount).HasColumnType("decimal(18,4)");
        b.Entity<CrmContractItem>().Property(x => x.Quantity).HasColumnType("decimal(18,4)");
        b.Entity<CrmContractItem>().Property(x => x.UnitPrice).HasColumnType("decimal(18,4)");
        b.Entity<CrmContractItem>().Property(x => x.Amount).HasColumnType("decimal(18,4)");
        b.Entity<CrmContract>().Property(x => x.TotalAmount).HasColumnType("decimal(18,4)");
        b.Entity<CrmPayment>().Property(x => x.Amount).HasColumnType("decimal(18,4)");

        // 唯一索引
        b.Entity<CrmSyncUser>().HasIndex(x => x.UmcUserId).IsUnique();

        // 关系配置
        b.Entity<CrmCustomer>()
            .HasMany(x => x.Contacts)
            .WithOne(x => x.Customer)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<CrmCustomer>()
            .HasMany(x => x.Inquiries)
            .WithOne(x => x.Customer)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Entity<CrmInquiry>()
            .HasMany(x => x.Items)
            .WithOne(x => x.Inquiry)
            .HasForeignKey(x => x.InquiryId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<CrmQuote>()
            .HasMany(x => x.Items)
            .WithOne(x => x.Quote)
            .HasForeignKey(x => x.QuoteId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<CrmContract>()
            .HasMany(x => x.Items)
            .WithOne(x => x.Contract)
            .HasForeignKey(x => x.ContractId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<CrmContract>()
            .HasMany(x => x.Payments)
            .WithOne(x => x.Contract)
            .HasForeignKey(x => x.ContractId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
