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
    }
}
