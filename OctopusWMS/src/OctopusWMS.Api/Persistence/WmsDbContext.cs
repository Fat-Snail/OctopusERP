using Microsoft.EntityFrameworkCore;

namespace OctopusWMS.Api.Persistence;

public class WmsDbContext : DbContext
{
    public WmsDbContext(DbContextOptions<WmsDbContext> options) : base(options) { }

    public DbSet<WmsSyncUser> SyncUsers => Set<WmsSyncUser>();
    public DbSet<WmsWarehouse> Warehouses => Set<WmsWarehouse>();
    public DbSet<WmsLocation> Locations => Set<WmsLocation>();
    public DbSet<WmsInventory> Inventories => Set<WmsInventory>();
    public DbSet<WmsInboundOrder> InboundOrders => Set<WmsInboundOrder>();
    public DbSet<WmsInboundItem> InboundItems => Set<WmsInboundItem>();
    public DbSet<WmsOutboundOrder> OutboundOrders => Set<WmsOutboundOrder>();
    public DbSet<WmsOutboundItem> OutboundItems => Set<WmsOutboundItem>();
    public DbSet<WmsStocktake> Stocktakes => Set<WmsStocktake>();
    public DbSet<WmsStocktakeItem> StocktakeItems => Set<WmsStocktakeItem>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<WmsSyncUser>().ToTable("wms_sync_user");
        b.Entity<WmsWarehouse>().ToTable("wms_warehouse");
        b.Entity<WmsLocation>().ToTable("wms_location");
        b.Entity<WmsInventory>().ToTable("wms_inventory");
        b.Entity<WmsInboundOrder>().ToTable("wms_inbound_order");
        b.Entity<WmsInboundItem>().ToTable("wms_inbound_item");
        b.Entity<WmsOutboundOrder>().ToTable("wms_outbound_order");
        b.Entity<WmsOutboundItem>().ToTable("wms_outbound_item");
        b.Entity<WmsStocktake>().ToTable("wms_stocktake");
        b.Entity<WmsStocktakeItem>().ToTable("wms_stocktake_item");

        b.Entity<WmsSyncUser>().HasKey(x => x.Id);
        b.Entity<WmsSyncUser>().Property(x => x.Id).ValueGeneratedOnAdd();
        b.Entity<WmsSyncUser>().HasIndex(x => x.UmcUserId).IsUnique();

        b.Entity<WmsWarehouse>().HasKey(x => x.WarehouseId);
        b.Entity<WmsWarehouse>().Property(x => x.WarehouseId).ValueGeneratedOnAdd();

        b.Entity<WmsLocation>().HasKey(x => x.LocationId);
        b.Entity<WmsLocation>().Property(x => x.LocationId).ValueGeneratedOnAdd();
        b.Entity<WmsLocation>().Property(x => x.Capacity).HasColumnType("decimal(18,4)");

        b.Entity<WmsInventory>().HasKey(x => x.InventoryId);
        b.Entity<WmsInventory>().Property(x => x.InventoryId).ValueGeneratedOnAdd();
        b.Entity<WmsInventory>().Property(x => x.Quantity).HasColumnType("decimal(18,4)");
        b.Entity<WmsInventory>().Property(x => x.LockedQuantity).HasColumnType("decimal(18,4)");
        b.Entity<WmsInventory>().Property(x => x.SafetyStock).HasColumnType("decimal(18,4)");
        b.Entity<WmsInventory>().HasIndex(x => new { x.WarehouseId, x.PlmProductId });

        b.Entity<WmsInboundOrder>().HasKey(x => x.InboundId);
        b.Entity<WmsInboundOrder>().Property(x => x.InboundId).ValueGeneratedOnAdd();

        b.Entity<WmsInboundItem>().HasKey(x => x.ItemId);
        b.Entity<WmsInboundItem>().Property(x => x.ItemId).ValueGeneratedOnAdd();
        b.Entity<WmsInboundItem>().Property(x => x.ExpectedQty).HasColumnType("decimal(18,4)");
        b.Entity<WmsInboundItem>().Property(x => x.ReceivedQty).HasColumnType("decimal(18,4)");

        b.Entity<WmsOutboundOrder>().HasKey(x => x.OutboundId);
        b.Entity<WmsOutboundOrder>().Property(x => x.OutboundId).ValueGeneratedOnAdd();

        b.Entity<WmsOutboundItem>().HasKey(x => x.ItemId);
        b.Entity<WmsOutboundItem>().Property(x => x.ItemId).ValueGeneratedOnAdd();
        b.Entity<WmsOutboundItem>().Property(x => x.RequestedQty).HasColumnType("decimal(18,4)");
        b.Entity<WmsOutboundItem>().Property(x => x.ShippedQty).HasColumnType("decimal(18,4)");

        b.Entity<WmsStocktake>().HasKey(x => x.StocktakeId);
        b.Entity<WmsStocktake>().Property(x => x.StocktakeId).ValueGeneratedOnAdd();

        b.Entity<WmsStocktakeItem>().HasKey(x => x.ItemId);
        b.Entity<WmsStocktakeItem>().Property(x => x.ItemId).ValueGeneratedOnAdd();
        b.Entity<WmsStocktakeItem>().Property(x => x.BookQty).HasColumnType("decimal(18,4)");
        b.Entity<WmsStocktakeItem>().Property(x => x.ActualQty).HasColumnType("decimal(18,4)");
        b.Entity<WmsStocktakeItem>().Property(x => x.DiffQty).HasColumnType("decimal(18,4)");

        b.Entity<WmsWarehouse>()
            .HasMany(x => x.Locations)
            .WithOne(x => x.Warehouse)
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<WmsInboundOrder>()
            .HasMany(x => x.Items)
            .WithOne(x => x.Inbound)
            .HasForeignKey(x => x.InboundId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<WmsOutboundOrder>()
            .HasMany(x => x.Items)
            .WithOne(x => x.Outbound)
            .HasForeignKey(x => x.OutboundId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<WmsStocktake>()
            .HasMany(x => x.Items)
            .WithOne(x => x.Stocktake)
            .HasForeignKey(x => x.StocktakeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
