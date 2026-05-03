using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OctopusUMC.Infrastructure.Persistence;

namespace OctopusUMC.Api.Tests;

/// <summary>
/// 测试用 WebApplicationFactory：使用 SQLite 内存数据库（共享连接），保持测试结果干净。
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    // 保持连接打开，确保 SQLite 内存库在测试期间不被回收
    private readonly SqliteConnection _connection;

    public TestWebApplicationFactory()
    {
        _connection = new SqliteConnection("Data Source=:memory:;Cache=Shared");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // 移除生产环境的 DbContext 注册
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (descriptor != null) services.Remove(descriptor);

            // 注册测试用 SQLite 内存库（共享连接保持存活��+ OpenIddict
            services.AddDbContext<ApplicationDbContext>(opt =>
            {
                opt.UseSqlite(_connection);
                opt.UseOpenIddict();
            });

            // 初始化数据库结构和种子数据
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Database.EnsureCreated();
            DbSeeder.Seed(db);
        });

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.SetMinimumLevel(LogLevel.Warning);
        });

        builder.UseEnvironment("Test");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
