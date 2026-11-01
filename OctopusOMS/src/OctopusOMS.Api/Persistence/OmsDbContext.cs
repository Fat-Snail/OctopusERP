using Microsoft.EntityFrameworkCore;

namespace OctopusOMS.Api.Persistence;

public class OmsDbContext : DbContext
{
    public OmsDbContext(DbContextOptions<OmsDbContext> options) : base(options) { }

    public DbSet<OmsChannel> Channels => Set<OmsChannel>();
    public DbSet<OmsShop> Shops => Set<OmsShop>();
    public DbSet<OmsOrder> Orders => Set<OmsOrder>();
    public DbSet<OmsOrderItem> OrderItems => Set<OmsOrderItem>();
    public DbSet<OmsSkuMapping> SkuMappings => Set<OmsSkuMapping>();
    public DbSet<OmsAfterSale> AfterSales => Set<OmsAfterSale>();
    public DbSet<OmsSyncUser> SyncUsers => Set<OmsSyncUser>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<OmsChannel>().ToTable("oms_channel");
        b.Entity<OmsChannel>().HasKey(x => x.ChannelId);
        b.Entity<OmsChannel>().Property(x => x.ChannelId).ValueGeneratedOnAdd();
        b.Entity<OmsChannel>().HasIndex(x => x.ChannelCode).IsUnique();

        b.Entity<OmsShop>().ToTable("oms_shop");
        b.Entity<OmsShop>().HasKey(x => x.ShopId);
        b.Entity<OmsShop>().Property(x => x.ShopId).ValueGeneratedOnAdd();
        b.Entity<OmsShop>().HasIndex(x => new { x.ChannelId, x.ShopCode }).IsUnique();

        b.Entity<OmsOrder>().ToTable("oms_order");
        b.Entity<OmsOrder>().HasKey(x => x.OrderId);
        b.Entity<OmsOrder>().Property(x => x.OrderId).ValueGeneratedOnAdd();
        b.Entity<OmsOrder>().HasIndex(x => x.OrderNo).IsUnique();
        b.Entity<OmsOrder>().HasIndex(x => new { x.ShopId, x.ExternalOrderNo }).IsUnique();
        b.Entity<OmsOrder>().Property(x => x.TotalAmount).HasColumnType("decimal(18,2)");
        b.Entity<OmsOrder>().Property(x => x.PaidAmount).HasColumnType("decimal(18,2)");

        b.Entity<OmsOrderItem>().ToTable("oms_order_item");
        b.Entity<OmsOrderItem>().HasKey(x => x.ItemId);
        b.Entity<OmsOrderItem>().Property(x => x.ItemId).ValueGeneratedOnAdd();
        b.Entity<OmsOrderItem>().Property(x => x.Quantity).HasColumnType("decimal(18,4)");
        b.Entity<OmsOrderItem>().Property(x => x.UnitPrice).HasColumnType("decimal(18,2)");
        b.Entity<OmsOrderItem>().Property(x => x.Amount).HasColumnType("decimal(18,2)");

        b.Entity<OmsSkuMapping>().ToTable("oms_sku_mapping");
        b.Entity<OmsSkuMapping>().HasKey(x => x.MappingId);
        b.Entity<OmsSkuMapping>().Property(x => x.MappingId).ValueGeneratedOnAdd();
        b.Entity<OmsSkuMapping>().HasIndex(x => new { x.ChannelId, x.ChannelSkuCode }).IsUnique();

        b.Entity<OmsAfterSale>().ToTable("oms_after_sale");
        b.Entity<OmsAfterSale>().HasKey(x => x.AfterSaleId);
        b.Entity<OmsAfterSale>().Property(x => x.AfterSaleId).ValueGeneratedOnAdd();
        b.Entity<OmsAfterSale>().HasIndex(x => x.AfterSaleNo).IsUnique();
        b.Entity<OmsAfterSale>().Property(x => x.Amount).HasColumnType("decimal(18,2)");

        b.Entity<OmsSyncUser>().ToTable("oms_sync_user");
        b.Entity<OmsSyncUser>().HasKey(x => x.Id);
        b.Entity<OmsSyncUser>().Property(x => x.Id).ValueGeneratedOnAdd();
        b.Entity<OmsSyncUser>().HasIndex(x => x.UmcUserId).IsUnique();

        b.Entity<OmsShop>()
            .HasOne(x => x.Channel)
            .WithMany()
            .HasForeignKey(x => x.ChannelId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Entity<OmsOrder>()
            .HasMany(x => x.Items)
            .WithOne(x => x.Order)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
