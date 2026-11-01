using Microsoft.EntityFrameworkCore;

namespace MiniappTodo.Api.Persistence;

public class TodoDbContext(DbContextOptions<TodoDbContext> options) : DbContext(options)
{
    public DbSet<TodoItem> Todos => Set<TodoItem>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<TodoItem>(e =>
        {
            e.ToTable("todo_item");
            e.HasKey(x => x.TodoId);
            e.Property(x => x.TodoId).ValueGeneratedOnAdd();
            e.Property(x => x.KcSub).IsRequired().HasMaxLength(64);
            e.HasIndex(x => new { x.KcSub, x.IsDone });
            e.HasIndex(x => x.CreatedAt);
        });

        b.Entity<UserProfile>(e =>
        {
            e.ToTable("user_profile");
            e.HasKey(x => x.KcSub);
            e.Property(x => x.KcSub).HasMaxLength(64);
        });
    }
}
