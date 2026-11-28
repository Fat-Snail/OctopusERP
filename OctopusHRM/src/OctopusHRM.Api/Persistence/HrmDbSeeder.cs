using System.Text.Json;
using OctopusHRM.Api.Persistence;

namespace OctopusHRM.Api.Persistence;

public static class HrmDbSeeder
{
    public static void Seed(HrmDbContext db)
    {
        SeedBaseIfEmpty(db);
        SeedPayrollAndKpi(db); // 幂等：已有算薪/KPI 则跳过
        db.SaveChanges();
    }

    // ============ 基础数据（仅首次建库）============
    private static void SeedBaseIfEmpty(HrmDbContext db)
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

    // ============ 薪酬 + 绩效模拟数据（幂等，供工资核算/绩效看板演示）============
    public static void SeedPayrollAndKpi(HrmDbContext db)
    {
        if (!db.Employees.Any()) return;

        var employees = db.Employees.Where(e => e.Status != "resigned").OrderBy(e => e.EmployeeId).ToList();

        // 算薪批次（避开集成测试使用的 2026-11 / 2025-12 / 2027-01 期间）
        if (!db.PayrollRuns.Any())
        {
            const decimal meal = 600m, traffic = 400m;

            HrmPayrollDetail BuildDetail(HrmEmployee emp)
            {
                var baseSalary = emp.BaseSalary ?? 0m;
                var earnings = baseSalary + meal + traffic;
                var si = baseSalary * 0.105m;
                var hf = baseSalary * 0.08m;
                var taxable = baseSalary - si - hf - 5000m;
                var tax = taxable > 0 ? Math.Round(taxable * 0.05m, 2) : 0m;
                var deductions = si + hf + tax;
                var net = earnings - deductions;

                var items = new object[]
                {
                    new { code = "BASE", name = "基本工资", type = "earning", amount = baseSalary },
                    new { code = "MEAL", name = "餐补", type = "earning", amount = meal },
                    new { code = "TRAFFIC", name = "交通补贴", type = "earning", amount = traffic },
                    new { code = "SI", name = "社保个人", type = "deduction", amount = Math.Round(si, 2) },
                    new { code = "HF", name = "公积金个人", type = "deduction", amount = Math.Round(hf, 2) },
                    new { code = "TAX", name = "个人所得税", type = "deduction", amount = tax },
                };

                return new HrmPayrollDetail
                {
                    EmployeeId = emp.EmployeeId,
                    EmployeeNo = emp.EmployeeNo,
                    EmployeeName = emp.FullName,
                    AttendanceDays = 22,
                    LeaveDays = 0,
                    AbsentDays = 0,
                    ItemsJson = JsonSerializer.Serialize(items),
                    GrossPay = Math.Round(earnings, 2),
                    Deductions = Math.Round(deductions, 2),
                    NetPay = Math.Round(net, 2),
                };
            }

            HrmPayrollRun BuildRun(string code, int year, int month, string status, DateTime? paidAt)
            {
                var run = new HrmPayrollRun
                {
                    PeriodCode = code,
                    Year = year,
                    Month = month,
                    Status = status,
                    CreatedBy = 1,
                    CreatedAt = DateTime.UtcNow,
                    PaidAt = paidAt,
                };
                foreach (var emp in employees) run.Details.Add(BuildDetail(emp));
                run.EmployeeCount = run.Details.Count;
                run.TotalGrossPay = Math.Round(run.Details.Sum(d => d.GrossPay), 2);
                run.TotalNetPay = Math.Round(run.Details.Sum(d => d.NetPay), 2);
                return run;
            }

            db.PayrollRuns.AddRange(
                BuildRun("2026-09", 2026, 9, "paid", new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc)),
                BuildRun("2026-10", 2026, 10, "calculated", null)
            );
        }

        // KPI / 绩效评分（季度 + 月度）
        if (!db.Kpis.Any())
        {
            var now = DateTime.UtcNow;
            db.Kpis.AddRange(
                new HrmKpi { EmployeeId = 1, Period = "2026-Q3", Title = "季度绩效考核", Score = 92m, Grade = "S", Comment = "战略目标达成，团队管理出色", ReviewerId = 1, Status = "approved", CreatedAt = now },
                new HrmKpi { EmployeeId = 2, Period = "2026-Q3", Title = "季度绩效考核", Score = 85m, Grade = "A", Comment = "技术攻坚突出，交付质量高", ReviewerId = 1, Status = "approved", CreatedAt = now },
                new HrmKpi { EmployeeId = 3, Period = "2026-Q3", Title = "季度绩效考核", Score = 78m, Grade = "B", Comment = "稳步推进，仍有提升空间", ReviewerId = 1, Status = "approved", CreatedAt = now },
                new HrmKpi { EmployeeId = 1, Period = "2026-10", Title = "月度绩效", Score = 90m, Grade = "S", Comment = "本月目标超额完成", ReviewerId = 1, Status = "submitted", CreatedAt = now },
                new HrmKpi { EmployeeId = 2, Period = "2026-10", Title = "月度绩效", Score = 88m, Grade = "A", Comment = "关键模块按时交付", ReviewerId = 1, Status = "submitted", CreatedAt = now },
                new HrmKpi { EmployeeId = 3, Period = "2026-10", Title = "月度绩效", Score = 72m, Grade = "B", Comment = "完成基础任务", ReviewerId = 1, Status = "submitted", CreatedAt = now }
            );
        }
    }
}