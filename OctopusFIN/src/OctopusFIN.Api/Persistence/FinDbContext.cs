using Microsoft.EntityFrameworkCore;

namespace OctopusFIN.Api.Persistence;

public class FinDbContext : DbContext
{
    public FinDbContext(DbContextOptions<FinDbContext> options) : base(options) { }

    public DbSet<FinAccount> Accounts => Set<FinAccount>();
    public DbSet<FinPeriod> Periods => Set<FinPeriod>();
    public DbSet<FinVoucher> Vouchers => Set<FinVoucher>();
    public DbSet<FinVoucherEntry> VoucherEntries => Set<FinVoucherEntry>();
    public DbSet<FinArInvoice> ArInvoices => Set<FinArInvoice>();
    public DbSet<FinArReceipt> ArReceipts => Set<FinArReceipt>();
    public DbSet<FinApInvoice> ApInvoices => Set<FinApInvoice>();
    public DbSet<FinApPayment> ApPayments => Set<FinApPayment>();
    public DbSet<FinSyncUser> SyncUsers => Set<FinSyncUser>();
    public DbSet<FinOrganization> Organizations => Set<FinOrganization>();
    public DbSet<FinProfitCenter> ProfitCenters => Set<FinProfitCenter>();
    public DbSet<FinInvoice> Invoices => Set<FinInvoice>();
    public DbSet<FinInvoiceItem> InvoiceItems => Set<FinInvoiceItem>();
    public DbSet<FinTaxReturn> TaxReturns => Set<FinTaxReturn>();
    public DbSet<FinBankAccount> BankAccounts => Set<FinBankAccount>();
    public DbSet<FinBankStatement> BankStatements => Set<FinBankStatement>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<FinAccount>().ToTable("fin_account");
        b.Entity<FinPeriod>().ToTable("fin_period");
        b.Entity<FinVoucher>().ToTable("fin_voucher");
        b.Entity<FinVoucherEntry>().ToTable("fin_voucher_entry");
        b.Entity<FinArInvoice>().ToTable("fin_ar_invoice");
        b.Entity<FinArReceipt>().ToTable("fin_ar_receipt");
        b.Entity<FinApInvoice>().ToTable("fin_ap_invoice");
        b.Entity<FinApPayment>().ToTable("fin_ap_payment");
        b.Entity<FinSyncUser>().ToTable("fin_sync_user");
        b.Entity<FinOrganization>().ToTable("fin_organization");
        b.Entity<FinProfitCenter>().ToTable("fin_profit_center");
        b.Entity<FinInvoice>().ToTable("fin_invoice");
        b.Entity<FinInvoiceItem>().ToTable("fin_invoice_item");
        b.Entity<FinTaxReturn>().ToTable("fin_tax_return");
        b.Entity<FinBankAccount>().ToTable("fin_bank_account");
        b.Entity<FinBankStatement>().ToTable("fin_bank_statement");

        b.Entity<FinAccount>().HasKey(x => x.AccountId);
        b.Entity<FinAccount>().Property(x => x.AccountId).ValueGeneratedOnAdd();
        b.Entity<FinAccount>().HasIndex(x => x.AccountCode).IsUnique();

        b.Entity<FinPeriod>().HasKey(x => x.PeriodId);
        b.Entity<FinPeriod>().Property(x => x.PeriodId).ValueGeneratedOnAdd();
        b.Entity<FinPeriod>().HasIndex(x => x.PeriodCode).IsUnique();

        b.Entity<FinVoucher>().HasKey(x => x.VoucherId);
        b.Entity<FinVoucher>().Property(x => x.VoucherId).ValueGeneratedOnAdd();
        b.Entity<FinVoucher>().HasIndex(x => x.VoucherNo).IsUnique();
        b.Entity<FinVoucher>().Property(x => x.TotalDebit).HasColumnType("decimal(18,4)");
        b.Entity<FinVoucher>().Property(x => x.TotalCredit).HasColumnType("decimal(18,4)");

        b.Entity<FinVoucherEntry>().HasKey(x => x.EntryId);
        b.Entity<FinVoucherEntry>().Property(x => x.EntryId).ValueGeneratedOnAdd();
        b.Entity<FinVoucherEntry>().Property(x => x.DebitAmount).HasColumnType("decimal(18,4)");
        b.Entity<FinVoucherEntry>().Property(x => x.CreditAmount).HasColumnType("decimal(18,4)");

        b.Entity<FinVoucher>()
            .HasMany(x => x.Entries)
            .WithOne(x => x.Voucher)
            .HasForeignKey(x => x.VoucherId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<FinArInvoice>().HasKey(x => x.InvoiceId);
        b.Entity<FinArInvoice>().Property(x => x.InvoiceId).ValueGeneratedOnAdd();
        b.Entity<FinArInvoice>().Property(x => x.Amount).HasColumnType("decimal(18,4)");
        b.Entity<FinArInvoice>().Property(x => x.ReceivedAmount).HasColumnType("decimal(18,4)");
        b.Entity<FinArInvoice>().HasIndex(x => x.InvoiceNo).IsUnique();

        b.Entity<FinArReceipt>().HasKey(x => x.ReceiptId);
        b.Entity<FinArReceipt>().Property(x => x.ReceiptId).ValueGeneratedOnAdd();
        b.Entity<FinArReceipt>().Property(x => x.Amount).HasColumnType("decimal(18,4)");

        b.Entity<FinApInvoice>().HasKey(x => x.InvoiceId);
        b.Entity<FinApInvoice>().Property(x => x.InvoiceId).ValueGeneratedOnAdd();
        b.Entity<FinApInvoice>().Property(x => x.Amount).HasColumnType("decimal(18,4)");
        b.Entity<FinApInvoice>().Property(x => x.PaidAmount).HasColumnType("decimal(18,4)");
        b.Entity<FinApInvoice>().HasIndex(x => x.InvoiceNo).IsUnique();

        b.Entity<FinApPayment>().HasKey(x => x.PaymentId);
        b.Entity<FinApPayment>().Property(x => x.PaymentId).ValueGeneratedOnAdd();
        b.Entity<FinApPayment>().Property(x => x.Amount).HasColumnType("decimal(18,4)");

        b.Entity<FinSyncUser>().HasKey(x => x.Id);
        b.Entity<FinSyncUser>().Property(x => x.Id).ValueGeneratedOnAdd();
        b.Entity<FinSyncUser>().HasIndex(x => x.UmcUserId).IsUnique();

        b.Entity<FinOrganization>().HasKey(x => x.OrganizationId);
        b.Entity<FinOrganization>().Property(x => x.OrganizationId).ValueGeneratedOnAdd();
        b.Entity<FinOrganization>().HasIndex(x => x.OrgCode).IsUnique();

        b.Entity<FinProfitCenter>().HasKey(x => x.ProfitCenterId);
        b.Entity<FinProfitCenter>().Property(x => x.ProfitCenterId).ValueGeneratedOnAdd();
        b.Entity<FinProfitCenter>().HasIndex(x => x.CenterCode).IsUnique();
        b.Entity<FinProfitCenter>()
            .HasOne(x => x.Organization)
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        // 凭证维度：按组织建索引，加速多组织报表查询
        b.Entity<FinVoucher>().HasIndex(x => x.OrganizationId);
        // 分录维度：按利润中心建索引，加速阿米巴报表下钻
        b.Entity<FinVoucherEntry>().HasIndex(x => x.ProfitCenterId);

        b.Entity<FinInvoice>().HasKey(x => x.InvoiceId);
        b.Entity<FinInvoice>().Property(x => x.InvoiceId).ValueGeneratedOnAdd();
        b.Entity<FinInvoice>().HasIndex(x => x.InvoiceNo).IsUnique();
        b.Entity<FinInvoice>().Property(x => x.Amount).HasColumnType("decimal(18,4)");
        b.Entity<FinInvoice>().Property(x => x.TaxAmount).HasColumnType("decimal(18,4)");
        b.Entity<FinInvoice>().Property(x => x.TotalAmount).HasColumnType("decimal(18,4)");
        b.Entity<FinInvoice>()
            .HasMany(x => x.Items)
            .WithOne(x => x.Invoice)
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<FinInvoiceItem>().HasKey(x => x.ItemId);
        b.Entity<FinInvoiceItem>().Property(x => x.ItemId).ValueGeneratedOnAdd();
        b.Entity<FinInvoiceItem>().Property(x => x.Amount).HasColumnType("decimal(18,4)");
        b.Entity<FinInvoiceItem>().Property(x => x.TaxAmount).HasColumnType("decimal(18,4)");

        b.Entity<FinTaxReturn>().HasKey(x => x.ReturnId);
        b.Entity<FinTaxReturn>().Property(x => x.ReturnId).ValueGeneratedOnAdd();
        b.Entity<FinTaxReturn>().Property(x => x.TaxableAmount).HasColumnType("decimal(18,4)");
        b.Entity<FinTaxReturn>().Property(x => x.TaxPayable).HasColumnType("decimal(18,4)");
        b.Entity<FinTaxReturn>().Property(x => x.TaxPaid).HasColumnType("decimal(18,4)");
        b.Entity<FinTaxReturn>().Property(x => x.TaxRefund).HasColumnType("decimal(18,4)");

        b.Entity<FinBankAccount>().HasKey(x => x.AccountId);
        b.Entity<FinBankAccount>().Property(x => x.AccountId).ValueGeneratedOnAdd();
        b.Entity<FinBankAccount>().HasIndex(x => x.AccountNo).IsUnique();

        b.Entity<FinBankStatement>().HasKey(x => x.StatementId);
        b.Entity<FinBankStatement>().Property(x => x.StatementId).ValueGeneratedOnAdd();
        b.Entity<FinBankStatement>().HasIndex(x => x.ExternalTxnId);
        b.Entity<FinBankStatement>().Property(x => x.Amount).HasColumnType("decimal(18,4)");
    }
}
