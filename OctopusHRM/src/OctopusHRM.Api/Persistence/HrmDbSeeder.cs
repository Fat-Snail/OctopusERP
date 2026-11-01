namespace OctopusHRM.Api.Persistence;

public static class HrmDbSeeder
{
    public static void Seed(HrmDbContext db)
    {
        if (db.Employees.Any()) return;
        var now = DateTime.UtcNow;

        db.Employees.AddRange(
            new HrmEmployee { EmployeeId = 1, OaEmployeeId = 1, UmcUserId = 1, EmployeeNo = "EMP-001", FullName = "管理员", Department = "总裁办", Position = "总经理", EmploymentType = "full_time", HiredAt = now.AddYears(-5), RegularizedAt = now.AddYears(-5).AddMonths(3), Status = "active", BaseSalary = 30000m, Phone = "13800138000", Email = "admin@octopus.com", CreatedAt = now, UpdatedAt = now },
            new HrmEmployee { EmployeeId = 2, OaEmployeeId = 2, UmcUserId = 2, EmployeeNo = "EMP-002", FullName = "张三", Department = "技术部", Position = "高级开发", EmploymentType = "full_time", HiredAt = now.AddYears(-3), RegularizedAt = now.AddYears(-3).AddMonths(3), Status = "active", BaseSalary = 18000m, Phone = "13800138001", Email = "zhangsan@octopus.com", CreatedAt = now, UpdatedAt = now },
            new HrmEmployee { EmployeeId = 3, OaEmployeeId = 3, UmcUserId = 3, EmployeeNo = "EMP-003", FullName = "李四", Department = "技术部", Position = "中级开发", EmploymentType = "full_time", HiredAt = now.AddYears(-1), Status = "active", BaseSalary = 12000m, Phone = "13800138002", Email = "lisi@octopus.com", CreatedAt = now, UpdatedAt = now }
        );

        db.SalaryItems.AddRange(
            new HrmSalaryItem { ItemId = 1, ItemCode = "BASE", ItemName = "基本工资", ItemType = "earning", CalculationType = "fixed", IsTaxable = true, Status = "active" },
            new HrmSalaryItem { ItemId = 2, ItemCode = "PERF", ItemName = "绩效奖金", ItemType = "earning", CalculationType = "manual", IsTaxable = true, Status = "active" },
            new HrmSalaryItem { ItemId = 3, ItemCode = "MEAL", ItemName = "餐补", ItemType = "earning", CalculationType = "fixed", FixedAmount = 600m, IsTaxable = false, Status = "active" },
            new HrmSalaryItem { ItemId = 4, ItemCode = "TRAFFIC", ItemName = "交通补贴", ItemType = "earning", CalculationType = "fixed", FixedAmount = 400m, IsTaxable = false, Status = "active" },
            new HrmSalaryItem { ItemId = 5, ItemCode = "SI", ItemName = "社保个人", ItemType = "deduction", CalculationType = "formula", Formula = "base * 0.105", IsTaxable = false, Status = "active" },
            new HrmSalaryItem { ItemId = 6, ItemCode = "HF", ItemName = "公积金个人", ItemType = "deduction", CalculationType = "formula", Formula = "base * 0.08", IsTaxable = false, Status = "active" },
            new HrmSalaryItem { ItemId = 7, ItemCode = "TAX", ItemName = "个人所得税", ItemType = "deduction", CalculationType = "manual", IsTaxable = false, Status = "active" }
        );

        db.LeaveBalances.AddRange(
            new HrmLeaveBalance { BalanceId = 1, EmployeeId = 2, LeaveType = "annual", Year = now.Year, TotalDays = 10, UsedDays = 2, RemainingDays = 8, UpdatedAt = now },
            new HrmLeaveBalance { BalanceId = 2, EmployeeId = 2, LeaveType = "sick", Year = now.Year, TotalDays = 5, UsedDays = 0, RemainingDays = 5, UpdatedAt = now },
            new HrmLeaveBalance { BalanceId = 3, EmployeeId = 3, LeaveType = "annual", Year = now.Year, TotalDays = 5, UsedDays = 0, RemainingDays = 5, UpdatedAt = now }
        );

        db.SyncUsers.AddRange(
            new HrmSyncUser { Id = 1, UmcUserId = 1, UserName = "admin", NickName = "超级管理员", Email = "admin@octopus.com", LastSyncAt = now },
            new HrmSyncUser { Id = 2, UmcUserId = 2, UserName = "zhangsan", NickName = "张三", Email = "zhangsan@octopus.com", LastSyncAt = now },
            new HrmSyncUser { Id = 3, UmcUserId = 3, UserName = "lisi", NickName = "李四", Email = "lisi@octopus.com", LastSyncAt = now }
        );

        db.SaveChanges();
    }
}
