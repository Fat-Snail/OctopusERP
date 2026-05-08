using Microsoft.EntityFrameworkCore;

namespace OctopusMES.Api.Persistence;

public class MesDbContext : DbContext
{
    public MesDbContext(DbContextOptions<MesDbContext> options) : base(options) { }

    public DbSet<MesSyncUser> SyncUsers => Set<MesSyncUser>();
    public DbSet<MesSupplier> Suppliers => Set<MesSupplier>();
    public DbSet<MesPurchaseOrder> PurchaseOrders => Set<MesPurchaseOrder>();
    public DbSet<MesPurchaseItem> PurchaseItems => Set<MesPurchaseItem>();
    public DbSet<MesWorkOrder> WorkOrders => Set<MesWorkOrder>();
    public DbSet<MesWorkOrderProcess> WorkOrderProcesses => Set<MesWorkOrderProcess>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<MesSyncUser>().ToTable("mes_sync_user");
        b.Entity<MesSupplier>().ToTable("mes_supplier");
        b.Entity<MesPurchaseOrder>().ToTable("mes_purchase_order");
        b.Entity<MesPurchaseItem>().ToTable("mes_purchase_item");
        b.Entity<MesWorkOrder>().ToTable("mes_work_order");
        b.Entity<MesWorkOrderProcess>().ToTable("mes_work_order_process");

        b.Entity<MesSyncUser>().HasKey(x => x.Id);
        b.Entity<MesSyncUser>().Property(x => x.Id).ValueGeneratedOnAdd();
        b.Entity<MesSyncUser>().HasIndex(x => x.UmcUserId).IsUnique();

        b.Entity<MesSupplier>().HasKey(x => x.SupplierId);
        b.Entity<MesSupplier>().Property(x => x.SupplierId).ValueGeneratedOnAdd();

        b.Entity<MesPurchaseOrder>().HasKey(x => x.PurchaseId);
        b.Entity<MesPurchaseOrder>().Property(x => x.PurchaseId).ValueGeneratedOnAdd();
        b.Entity<MesPurchaseOrder>().Property(x => x.TotalAmount).HasColumnType("decimal(18,4)");

        b.Entity<MesPurchaseItem>().HasKey(x => x.ItemId);
        b.Entity<MesPurchaseItem>().Property(x => x.ItemId).ValueGeneratedOnAdd();
        b.Entity<MesPurchaseItem>().Property(x => x.Quantity).HasColumnType("decimal(18,4)");
        b.Entity<MesPurchaseItem>().Property(x => x.UnitPrice).HasColumnType("decimal(18,4)");
        b.Entity<MesPurchaseItem>().Property(x => x.Amount).HasColumnType("decimal(18,4)");

        b.Entity<MesWorkOrder>().HasKey(x => x.WorkOrderId);
        b.Entity<MesWorkOrder>().Property(x => x.WorkOrderId).ValueGeneratedOnAdd();
        b.Entity<MesWorkOrder>().Property(x => x.PlannedQty).HasColumnType("decimal(18,4)");
        b.Entity<MesWorkOrder>().Property(x => x.CompletedQty).HasColumnType("decimal(18,4)");

        b.Entity<MesWorkOrderProcess>().HasKey(x => x.ProcessId);
        b.Entity<MesWorkOrderProcess>().Property(x => x.ProcessId).ValueGeneratedOnAdd();

        b.Entity<MesSupplier>()
            .HasMany(x => x.PurchaseOrders)
            .WithOne(x => x.Supplier)
            .HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Entity<MesPurchaseOrder>()
            .HasMany(x => x.Items)
            .WithOne(x => x.PurchaseOrder)
            .HasForeignKey(x => x.PurchaseId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<MesWorkOrder>()
            .HasMany(x => x.Processes)
            .WithOne(x => x.WorkOrder)
            .HasForeignKey(x => x.WorkOrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
