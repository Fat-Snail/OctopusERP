using Microsoft.EntityFrameworkCore;

namespace OctopusHRM.Api.Persistence;

public class HrmDbContext : DbContext
{
    public HrmDbContext(DbContextOptions<HrmDbContext> options) : base(options) { }

    public DbSet<HrmEmployee> Employees => Set<HrmEmployee>();
    public DbSet<HrmSalaryItem> SalaryItems => Set<HrmSalaryItem>();
    public DbSet<HrmPayrollRun> PayrollRuns => Set<HrmPayrollRun>();
    public DbSet<HrmPayrollDetail> PayrollDetails => Set<HrmPayrollDetail>();
    public DbSet<HrmLeaveBalance> LeaveBalances => Set<HrmLeaveBalance>();
    public DbSet<HrmKpi> Kpis => Set<HrmKpi>();
    public DbSet<HrmSyncUser> SyncUsers => Set<HrmSyncUser>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<HrmEmployee>().ToTable("hrm_employee");
        b.Entity<HrmEmployee>().HasKey(x => x.EmployeeId);
        b.Entity<HrmEmployee>().Property(x => x.EmployeeId).ValueGeneratedOnAdd();
        b.Entity<HrmEmployee>().HasIndex(x => x.EmployeeNo).IsUnique();
        b.Entity<HrmEmployee>().Property(x => x.BaseSalary).HasColumnType("decimal(18,2)");

        b.Entity<HrmSalaryItem>().ToTable("hrm_salary_item");
        b.Entity<HrmSalaryItem>().HasKey(x => x.ItemId);
        b.Entity<HrmSalaryItem>().Property(x => x.ItemId).ValueGeneratedOnAdd();
        b.Entity<HrmSalaryItem>().HasIndex(x => x.ItemCode).IsUnique();
        b.Entity<HrmSalaryItem>().Property(x => x.FixedAmount).HasColumnType("decimal(18,2)");

        b.Entity<HrmPayrollRun>().ToTable("hrm_payroll_run");
        b.Entity<HrmPayrollRun>().HasKey(x => x.PayrollRunId);
        b.Entity<HrmPayrollRun>().Property(x => x.PayrollRunId).ValueGeneratedOnAdd();
        b.Entity<HrmPayrollRun>().HasIndex(x => x.PeriodCode).IsUnique();
        b.Entity<HrmPayrollRun>().Property(x => x.TotalGrossPay).HasColumnType("decimal(18,2)");
        b.Entity<HrmPayrollRun>().Property(x => x.TotalNetPay).HasColumnType("decimal(18,2)");

        b.Entity<HrmPayrollDetail>().ToTable("hrm_payroll_detail");
        b.Entity<HrmPayrollDetail>().HasKey(x => x.DetailId);
        b.Entity<HrmPayrollDetail>().Property(x => x.DetailId).ValueGeneratedOnAdd();
        b.Entity<HrmPayrollDetail>().Property(x => x.GrossPay).HasColumnType("decimal(18,2)");
        b.Entity<HrmPayrollDetail>().Property(x => x.Deductions).HasColumnType("decimal(18,2)");
        b.Entity<HrmPayrollDetail>().Property(x => x.NetPay).HasColumnType("decimal(18,2)");
        b.Entity<HrmPayrollDetail>().Property(x => x.AttendanceDays).HasColumnType("decimal(8,2)");
        b.Entity<HrmPayrollDetail>().Property(x => x.LeaveDays).HasColumnType("decimal(8,2)");
        b.Entity<HrmPayrollDetail>().Property(x => x.AbsentDays).HasColumnType("decimal(8,2)");

        b.Entity<HrmPayrollRun>()
            .HasMany(x => x.Details)
            .WithOne(x => x.PayrollRun)
            .HasForeignKey(x => x.PayrollRunId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<HrmLeaveBalance>().ToTable("hrm_leave_balance");
        b.Entity<HrmLeaveBalance>().HasKey(x => x.BalanceId);
        b.Entity<HrmLeaveBalance>().Property(x => x.BalanceId).ValueGeneratedOnAdd();
        b.Entity<HrmLeaveBalance>().HasIndex(x => new { x.EmployeeId, x.LeaveType, x.Year }).IsUnique();
        b.Entity<HrmLeaveBalance>().Property(x => x.TotalDays).HasColumnType("decimal(8,2)");
        b.Entity<HrmLeaveBalance>().Property(x => x.UsedDays).HasColumnType("decimal(8,2)");
        b.Entity<HrmLeaveBalance>().Property(x => x.RemainingDays).HasColumnType("decimal(8,2)");

        b.Entity<HrmKpi>().ToTable("hrm_kpi");
        b.Entity<HrmKpi>().HasKey(x => x.KpiId);
        b.Entity<HrmKpi>().Property(x => x.KpiId).ValueGeneratedOnAdd();
        b.Entity<HrmKpi>().Property(x => x.Score).HasColumnType("decimal(5,2)");

        b.Entity<HrmSyncUser>().ToTable("hrm_sync_user");
        b.Entity<HrmSyncUser>().HasKey(x => x.Id);
        b.Entity<HrmSyncUser>().Property(x => x.Id).ValueGeneratedOnAdd();
        b.Entity<HrmSyncUser>().HasIndex(x => x.UmcUserId).IsUnique();
    }
}
