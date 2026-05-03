using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using OctopusUMC.Core.Domain.Entities;
using System.Text.Json;

namespace OctopusUMC.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    // 主实体
    public DbSet<User>      Users      => Set<User>();
    public DbSet<Role>      Roles      => Set<Role>();
    public DbSet<Dept>      Depts      => Set<Dept>();
    public DbSet<Menu>      Menus      => Set<Menu>();
    public DbSet<Post>      Posts      => Set<Post>();
    public DbSet<DictType>  DictTypes  => Set<DictType>();
    public DbSet<DictData>  DictDatas  => Set<DictData>();
    public DbSet<Notice>    Notices    => Set<Notice>();
    public DbSet<Job>       Jobs       => Set<Job>();
    public DbSet<SysConfig> Configs    => Set<SysConfig>();
    public DbSet<OssFile>   OssFiles   => Set<OssFile>();
    public DbSet<OperLog>   OperLogs   => Set<OperLog>();
    public DbSet<LoginInfo> LoginInfos => Set<LoginInfo>();
    public DbSet<OidcClient> OidcClients => Set<OidcClient>();

    // 关联表
    public DbSet<UserDept> UserDepts => Set<UserDept>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RoleMenu> RoleMenus => Set<RoleMenu>();
    public DbSet<RoleDept> RoleDepts => Set<RoleDept>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // OpenIddict 实体表
        modelBuilder.UseOpenIddict();

        // MySQL utf8mb4 每字符 4 字节；IX_OpenIddictTokens_ApplicationId_Status_Subject_Type
        // 默认 Subject varchar(400) → 1600 字节，合计 3420 > 3072 上限，必须收窄
        modelBuilder.Entity<OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreApplication>(e =>
            e.Property(a => a.ClientId).HasMaxLength(100));
        modelBuilder.Entity<OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreToken>(e =>
            e.Property(t => t.Subject).HasMaxLength(200));
        modelBuilder.Entity<OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreAuthorization>(e =>
            e.Property(a => a.Subject).HasMaxLength(200));

        // ValueConverter for List<string> → JSON string (for OidcClient)
        var stringListConverter = new ValueConverter<List<string>, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>()
        );
        var stringListComparer = new ValueComparer<List<string>>(
            (l, r) => l != null && r != null && l.SequenceEqual(r),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v == null ? 0 : v.GetHashCode())),
            c => c.ToList()
        );

        // ── User ─────────────────────────────────────────────
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("sys_user");
            e.HasKey(u => u.UserId);
            e.Property(u => u.UserId).ValueGeneratedOnAdd();
            e.Ignore(u => u.UserDepts);
            e.Ignore(u => u.UserRoles);
        });

        // ── Role ─────────────────────────────────────────────
        modelBuilder.Entity<Role>(e =>
        {
            e.ToTable("sys_role");
            e.HasKey(r => r.RoleId);
            e.Property(r => r.RoleId).ValueGeneratedOnAdd();
            e.Ignore(r => r.RoleMenus);
            e.Ignore(r => r.RoleDepts);
        });

        // ── Dept ─────────────────────────────────────────────
        modelBuilder.Entity<Dept>(e =>
        {
            e.ToTable("sys_dept");
            e.HasKey(d => d.DeptId);
            e.Property(d => d.DeptId).ValueGeneratedOnAdd();
            e.Ignore(d => d.Children);
        });

        // ── Menu ─────────────────────────────────────────────
        modelBuilder.Entity<Menu>(e =>
        {
            e.ToTable("sys_menu");
            e.HasKey(m => m.MenuId);
            e.Property(m => m.MenuId).ValueGeneratedOnAdd();
            e.Ignore(m => m.Children);
        });

        // ── Post ─────────────────────────────────────────────
        modelBuilder.Entity<Post>(e =>
        {
            e.ToTable("sys_post");
            e.HasKey(p => p.PostId);
            e.Property(p => p.PostId).ValueGeneratedOnAdd();
        });

        // ── UserDept（关联表）────────────────────────────────
        modelBuilder.Entity<UserDept>(e =>
        {
            e.ToTable("sys_user_dept");
            e.HasKey(ud => new { ud.UserId, ud.DeptId });
        });

        // ── UserRole（关联表）────────────────────────────────
        modelBuilder.Entity<UserRole>(e =>
        {
            e.ToTable("sys_user_role");
            e.HasKey(ur => new { ur.UserId, ur.RoleId });
        });

        // ── RoleMenu（关联表）────────────────────────────────
        modelBuilder.Entity<RoleMenu>(e =>
        {
            e.ToTable("sys_role_menu");
            e.HasKey(rm => new { rm.RoleId, rm.MenuId });
        });

        // ── RoleDept（关联表）────────────────────────────────
        modelBuilder.Entity<RoleDept>(e =>
        {
            e.ToTable("sys_role_dept");
            e.HasKey(rd => new { rd.RoleId, rd.DeptId });
        });

        // ── DictType ─────────────────────────────────────────
        modelBuilder.Entity<DictType>(e =>
        {
            e.ToTable("sys_dict_type");
            e.HasKey(t => t.DictId);
            e.Property(t => t.DictId).ValueGeneratedOnAdd();
        });

        // ── DictData ─────────────────────────────────────────
        modelBuilder.Entity<DictData>(e =>
        {
            e.ToTable("sys_dict_data");
            e.HasKey(d => d.DictCode);
            e.Property(d => d.DictCode).ValueGeneratedOnAdd();
        });

        // ── Notice ───────────────────────────────────────────
        modelBuilder.Entity<Notice>(e =>
        {
            e.ToTable("sys_notice");
            e.HasKey(n => n.NoticeId);
            e.Property(n => n.NoticeId).ValueGeneratedOnAdd();
        });

        // ── Job ──────────────────────────────────────────────
        modelBuilder.Entity<Job>(e =>
        {
            e.ToTable("sys_job");
            e.HasKey(j => j.JobId);
            e.Property(j => j.JobId).ValueGeneratedOnAdd();
        });

        // ── SysConfig ────────────────────────────────────────
        modelBuilder.Entity<SysConfig>(e =>
        {
            e.ToTable("sys_config");
            e.HasKey(c => c.ConfigId);
            e.Property(c => c.ConfigId).ValueGeneratedOnAdd();
        });

        // ── OssFile ──────────────────────────────────────────
        modelBuilder.Entity<OssFile>(e =>
        {
            e.ToTable("sys_oss");
            e.HasKey(f => f.OssId);
            e.Property(f => f.OssId).ValueGeneratedOnAdd();
        });

        // ── OperLog ──────────────────────────────────────────
        modelBuilder.Entity<OperLog>(e =>
        {
            e.ToTable("sys_oper_log");
            e.HasKey(l => l.OperId);
            e.Property(l => l.OperId).ValueGeneratedOnAdd();
        });

        // ── LoginInfo ────────────────────────────────────────
        modelBuilder.Entity<LoginInfo>(e =>
        {
            e.ToTable("sys_logininfor");
            e.HasKey(l => l.InfoId);
            e.Property(l => l.InfoId).ValueGeneratedOnAdd();
        });

        // ── OidcClient ───────────────────────────────────────
        modelBuilder.Entity<OidcClient>(e =>
        {
            e.ToTable("tool_oidc_client");
            e.HasKey(c => c.Id);
            e.Property(c => c.RedirectUris)
             .HasConversion(stringListConverter, stringListComparer)
             .HasColumnType("TEXT");
            e.Property(c => c.PostLogoutRedirectUris)
             .HasConversion(stringListConverter, stringListComparer)
             .HasColumnType("TEXT");
        });
    }
}
