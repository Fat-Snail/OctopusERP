using Microsoft.EntityFrameworkCore;
using OctopusPLM.Core.Entities;

namespace OctopusPLM.Infrastructure.Persistence;

public class PlmDbContext : DbContext
{
    public PlmDbContext(DbContextOptions<PlmDbContext> options) : base(options) { }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<CategoryModelVersion> CategoryModelVersions => Set<CategoryModelVersion>();
    public DbSet<PlmAttribute> Attributes => Set<PlmAttribute>();
    public DbSet<AttributeValue> AttributeValues => Set<AttributeValue>();
    public DbSet<CategoryAttribute> CategoryAttributes => Set<CategoryAttribute>();
    public DbSet<RuleDef> RuleDefs => Set<RuleDef>();
    public DbSet<DetailTemplate> DetailTemplates => Set<DetailTemplate>();
    public DbSet<DetailComponentDef> DetailComponentDefs => Set<DetailComponentDef>();
    public DbSet<DetailComponentBind> DetailComponentBinds => Set<DetailComponentBind>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<ProductSku> ProductSkus => Set<ProductSku>();
    public DbSet<ProductAttributeValue> ProductAttributeValues => Set<ProductAttributeValue>();
    public DbSet<ProductDetailBlock> ProductDetailBlocks => Set<ProductDetailBlock>();
    public DbSet<ProductReview> ProductReviews => Set<ProductReview>();
    public DbSet<SyncUser> SyncUsers => Set<SyncUser>();
    public DbSet<ChannelDef> ChannelDefs => Set<ChannelDef>();
    public DbSet<ChannelCategoryMapping> ChannelCategoryMappings => Set<ChannelCategoryMapping>();
    public DbSet<ChannelAttributeMapping> ChannelAttributeMappings => Set<ChannelAttributeMapping>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Category - self-referencing tree
        modelBuilder.Entity<Category>(e =>
        {
            e.ToTable("plm_category");
            e.HasKey(x => x.CategoryId);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Path).HasMaxLength(500);
            e.Property(x => x.Icon).HasMaxLength(200);
            e.HasOne(x => x.Parent).WithMany(x => x.Children).HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        });

        // CategoryModelVersion
        modelBuilder.Entity<CategoryModelVersion>(e =>
        {
            e.ToTable("plm_category_model_version");
            e.HasKey(x => x.ModelVersionId);
            e.Property(x => x.VersionNo).HasMaxLength(50).IsRequired();
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.ChangeSummary).HasMaxLength(500);
            e.HasIndex(x => new { x.CategoryId, x.VersionNo }).IsUnique();
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Cascade);
        });

        // PlmAttribute
        modelBuilder.Entity<PlmAttribute>(e =>
        {
            e.ToTable("plm_attribute");
            e.HasKey(x => x.AttributeId);
            e.Property(x => x.Code).HasMaxLength(100);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.AttributeType).HasMaxLength(20).IsRequired();
            e.Property(x => x.InputType).HasMaxLength(20).IsRequired();
            e.Property(x => x.Unit).HasMaxLength(20);
            e.Property(x => x.ValueScope).HasMaxLength(20).IsRequired();
            e.HasIndex(x => x.Code).IsUnique();
        });

        // AttributeValue
        modelBuilder.Entity<AttributeValue>(e =>
        {
            e.ToTable("plm_attribute_value");
            e.HasKey(x => x.ValueId);
            e.Property(x => x.Label).HasMaxLength(100).IsRequired();
            e.Property(x => x.Value).HasMaxLength(100).IsRequired();
            e.HasOne(x => x.Attribute).WithMany(x => x.Values).HasForeignKey(x => x.AttributeId).OnDelete(DeleteBehavior.Cascade);
        });

        // CategoryAttribute
        modelBuilder.Entity<CategoryAttribute>(e =>
        {
            e.ToTable("plm_category_attribute");
            e.HasKey(x => x.Id);
            e.Property(x => x.GroupName).HasMaxLength(100);
            e.Property(x => x.GroupType).HasMaxLength(30).IsRequired();
            e.HasIndex(x => new { x.CategoryId, x.ModelVersionId, x.AttributeId }).IsUnique();
            e.HasOne(x => x.Category).WithMany(x => x.CategoryAttributes).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ModelVersion).WithMany(x => x.AttributeBinds).HasForeignKey(x => x.ModelVersionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Attribute).WithMany(x => x.CategoryAttributes).HasForeignKey(x => x.AttributeId).OnDelete(DeleteBehavior.Cascade);
        });

        // RuleDef
        modelBuilder.Entity<RuleDef>(e =>
        {
            e.ToTable("plm_rule_def");
            e.HasKey(x => x.RuleId);
            e.Property(x => x.RuleType).HasMaxLength(30).IsRequired();
            e.Property(x => x.RuleCode).HasMaxLength(100).IsRequired();
            e.Property(x => x.RuleName).HasMaxLength(100).IsRequired();
            e.Property(x => x.Message).HasMaxLength(500);
            e.HasIndex(x => new { x.ModelVersionId, x.RuleCode }).IsUnique();
            e.HasOne(x => x.ModelVersion).WithMany(x => x.Rules).HasForeignKey(x => x.ModelVersionId).OnDelete(DeleteBehavior.Cascade);
        });

        // DetailTemplate
        modelBuilder.Entity<DetailTemplate>(e =>
        {
            e.ToTable("plm_detail_template");
            e.HasKey(x => x.TemplateId);
            e.Property(x => x.TemplateName).HasMaxLength(100).IsRequired();
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.HasOne(x => x.ModelVersion).WithMany(x => x.DetailTemplates).HasForeignKey(x => x.ModelVersionId).OnDelete(DeleteBehavior.Cascade);
        });

        // DetailComponentDef
        modelBuilder.Entity<DetailComponentDef>(e =>
        {
            e.ToTable("plm_detail_component_def");
            e.HasKey(x => x.ComponentId);
            e.Property(x => x.ComponentCode).HasMaxLength(100).IsRequired();
            e.Property(x => x.ComponentName).HasMaxLength(100).IsRequired();
            e.Property(x => x.ComponentType).HasMaxLength(30).IsRequired();
            e.HasIndex(x => x.ComponentCode).IsUnique();
        });

        // DetailComponentBind
        modelBuilder.Entity<DetailComponentBind>(e =>
        {
            e.ToTable("plm_detail_component_bind");
            e.HasKey(x => x.BindId);
            e.HasIndex(x => new { x.TemplateId, x.ComponentId }).IsUnique();
            e.HasOne(x => x.Template).WithMany(x => x.ComponentBinds).HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Component).WithMany(x => x.TemplateBinds).HasForeignKey(x => x.ComponentId).OnDelete(DeleteBehavior.Cascade);
        });

        // Product
        modelBuilder.Entity<Product>(e =>
        {
            e.ToTable("plm_product");
            e.HasKey(x => x.ProductId);
            e.Property(x => x.ProductName).HasMaxLength(300).IsRequired();
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.MainImage).HasMaxLength(500);
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ModelVersion).WithMany(x => x.Products).HasForeignKey(x => x.ModelVersionId).OnDelete(DeleteBehavior.SetNull);
        });

        // ProductImage
        modelBuilder.Entity<ProductImage>(e =>
        {
            e.ToTable("plm_product_image");
            e.HasKey(x => x.ImageId);
            e.Property(x => x.Url).HasMaxLength(500).IsRequired();
            e.Property(x => x.ThumbnailUrl).HasMaxLength(500);
            e.Property(x => x.FeatureVectorHash).HasMaxLength(64);
            e.HasOne(x => x.Product).WithMany(x => x.Images).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        });

        // ProductSku
        modelBuilder.Entity<ProductSku>(e =>
        {
            e.ToTable("plm_product_sku");
            e.HasKey(x => x.SkuId);
            e.Property(x => x.SkuCode).HasMaxLength(100).IsRequired();
            e.Property(x => x.Barcode).HasMaxLength(100);
            e.Property(x => x.Price).HasColumnType("decimal(18,2)");
            e.Property(x => x.CostPrice).HasColumnType("decimal(18,2)");
            e.HasOne(x => x.Product).WithMany(x => x.Skus).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        });

        // ProductAttributeValue
        modelBuilder.Entity<ProductAttributeValue>(e =>
        {
            e.ToTable("plm_product_attribute_value");
            e.HasKey(x => x.Id);
            e.Property(x => x.ValueText).HasMaxLength(1000);
            e.Property(x => x.ValueNumber).HasColumnType("decimal(18,4)");
            e.HasIndex(x => new { x.ProductId, x.AttributeId }).IsUnique();
            e.HasOne(x => x.Product).WithMany(x => x.AttributeValues).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Attribute).WithMany().HasForeignKey(x => x.AttributeId).OnDelete(DeleteBehavior.Cascade);
        });

        // ProductDetailBlock
        modelBuilder.Entity<ProductDetailBlock>(e =>
        {
            e.ToTable("plm_product_detail_block");
            e.HasKey(x => x.BlockId);
            e.HasOne(x => x.Product).WithMany(x => x.DetailBlocks).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Component).WithMany(x => x.ProductBlocks).HasForeignKey(x => x.ComponentId).OnDelete(DeleteBehavior.Restrict);
        });

        // ProductReview
        modelBuilder.Entity<ProductReview>(e =>
        {
            e.ToTable("plm_product_review");
            e.HasKey(x => x.ReviewId);
            e.Property(x => x.Action).HasMaxLength(20).IsRequired();
            e.Property(x => x.Comment).HasMaxLength(500);
            e.HasOne(x => x.Product).WithMany(x => x.Reviews).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        });

        // SyncUser
        modelBuilder.Entity<SyncUser>(e =>
        {
            e.ToTable("plm_sync_user");
            e.HasKey(x => x.Id);
            e.Property(x => x.UserName).HasMaxLength(100).IsRequired();
            e.Property(x => x.NickName).HasMaxLength(100);
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.PhoneNumber).HasMaxLength(20);
            e.Property(x => x.PlmRoles).HasMaxLength(200);
            e.HasIndex(x => x.UmcUserId).IsUnique();
        });

        // ChannelDef
        modelBuilder.Entity<ChannelDef>(e =>
        {
            e.ToTable("plm_channel_def");
            e.HasKey(x => x.ChannelId);
            e.Property(x => x.ChannelCode).HasMaxLength(50).IsRequired();
            e.Property(x => x.ChannelName).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.ChannelCode).IsUnique();
        });

        // ChannelCategoryMapping
        modelBuilder.Entity<ChannelCategoryMapping>(e =>
        {
            e.ToTable("plm_channel_category_mapping");
            e.HasKey(x => x.MappingId);
            e.Property(x => x.ExternalCategoryId).HasMaxLength(100).IsRequired();
            e.Property(x => x.ExternalCategoryName).HasMaxLength(200).IsRequired();
            e.Property(x => x.MappingVersion).HasMaxLength(50).IsRequired();
            e.HasIndex(x => new { x.ChannelId, x.CategoryId, x.MappingVersion }).IsUnique();
            e.HasOne(x => x.Channel).WithMany(x => x.CategoryMappings).HasForeignKey(x => x.ChannelId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Cascade);
        });

        // ChannelAttributeMapping
        modelBuilder.Entity<ChannelAttributeMapping>(e =>
        {
            e.ToTable("plm_channel_attribute_mapping");
            e.HasKey(x => x.MappingId);
            e.Property(x => x.ExternalAttributeId).HasMaxLength(100).IsRequired();
            e.Property(x => x.ExternalAttributeName).HasMaxLength(200).IsRequired();
            e.HasIndex(x => new { x.ChannelCategoryMappingId, x.AttributeId }).IsUnique();
            e.HasOne(x => x.ChannelCategoryMapping).WithMany(x => x.AttributeMappings).HasForeignKey(x => x.ChannelCategoryMappingId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Attribute).WithMany().HasForeignKey(x => x.AttributeId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
