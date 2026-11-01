using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OctopusHRM.Api.Persistence;

namespace OctopusHRM.Api.Services;

public class PayrollService(HrmDbContext db, IHttpClientFactory httpFactory, IConfiguration config, ILogger<PayrollService> logger)
{
    public async Task<List<HrmPayrollRun>> GetRunsAsync() =>
        await db.PayrollRuns.OrderByDescending(x => x.PeriodCode).ToListAsync();

    public async Task<HrmPayrollRun?> GetRunAsync(long id) =>
        await db.PayrollRuns.Include(x => x.Details).FirstOrDefaultAsync(x => x.PayrollRunId == id);

    /// <summary>启动月度算薪（同期间已存在则返回原批次）</summary>
    public async Task<(HrmPayrollRun? run, string? error)> StartRunAsync(int year, int month, long createdBy)
    {
        if (month < 1 || month > 12) return (null, "月份必须在 1-12 之间");
        var code = $"{year}-{month:D2}";
        var existing = await db.PayrollRuns.FirstOrDefaultAsync(x => x.PeriodCode == code);
        if (existing != null) return (existing, null);

        var run = new HrmPayrollRun
        {
            PeriodCode = code,
            Year = year,
            Month = month,
            Status = "draft",
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
        };
        db.PayrollRuns.Add(run);
        await db.SaveChangesAsync();
        return (run, null);
    }

    /// <summary>计算批次的所有员工薪资（基本工资 + 餐补/交通 - 社保/公积金 - 个税近似 5%）</summary>
    public async Task<(HrmPayrollRun? run, string? error)> CalculateRunAsync(long runId)
    {
        var run = await db.PayrollRuns.Include(x => x.Details).FirstOrDefaultAsync(x => x.PayrollRunId == runId);
        if (run == null) return (null, "算薪批次不存在");
        if (run.Status != "draft") return (null, $"当前状态 {run.Status} 不能重新计算");

        // 清旧明细
        if (run.Details.Count > 0)
        {
            db.PayrollDetails.RemoveRange(run.Details);
            run.Details.Clear();
        }

        var employees = await db.Employees.Where(e => e.Status != "resigned").ToListAsync();
        var items = await db.SalaryItems.Where(x => x.Status == "active").ToListAsync();

        var meal = items.FirstOrDefault(x => x.ItemCode == "MEAL")?.FixedAmount ?? 0;
        var traffic = items.FirstOrDefault(x => x.ItemCode == "TRAFFIC")?.FixedAmount ?? 0;

        decimal totalGross = 0, totalNet = 0;
        foreach (var emp in employees)
        {
            var baseSalary = emp.BaseSalary ?? 0;
            var earnings = baseSalary + meal + traffic;
            var siDeduction = baseSalary * 0.105m;
            var hfDeduction = baseSalary * 0.08m;
            // 简化个税：超过 5000 部分 5%
            var taxable = baseSalary - siDeduction - hfDeduction - 5000m;
            var tax = taxable > 0 ? Math.Round(taxable * 0.05m, 2) : 0m;
            var deductions = siDeduction + hfDeduction + tax;
            var net = earnings - deductions;

            var detailItems = new[]
            {
                new { code = "BASE", name = "基本工资", type = "earning", amount = baseSalary },
                new { code = "MEAL", name = "餐补", type = "earning", amount = meal },
                new { code = "TRAFFIC", name = "交通补贴", type = "earning", amount = traffic },
                new { code = "SI", name = "社保个人", type = "deduction", amount = siDeduction },
                new { code = "HF", name = "公积金个人", type = "deduction", amount = hfDeduction },
                new { code = "TAX", name = "个人所得税", type = "deduction", amount = tax },
            };

            run.Details.Add(new HrmPayrollDetail
            {
                PayrollRunId = run.PayrollRunId,
                EmployeeId = emp.EmployeeId,
                EmployeeNo = emp.EmployeeNo,
                EmployeeName = emp.FullName,
                AttendanceDays = 22, // 默认；生产环境应从 OA 拉
                LeaveDays = 0,
                AbsentDays = 0,
                ItemsJson = JsonSerializer.Serialize(detailItems),
                GrossPay = Math.Round(earnings, 2),
                Deductions = Math.Round(deductions, 2),
                NetPay = Math.Round(net, 2),
            });
            totalGross += earnings;
            totalNet += net;
        }

        run.EmployeeCount = employees.Count;
        run.TotalGrossPay = Math.Round(totalGross, 2);
        run.TotalNetPay = Math.Round(totalNet, 2);
        run.Status = "calculated";

        await db.SaveChangesAsync();
        return (run, null);
    }

    /// <summary>核准 + 发薪：状态切换 + 推 FIN 生成薪资凭证</summary>
    public async Task<(HrmPayrollRun? run, string? error)> ApprovePayAsync(long runId)
    {
        var run = await db.PayrollRuns.FirstOrDefaultAsync(x => x.PayrollRunId == runId);
        if (run == null) return (null, "算薪批次不存在");
        if (run.Status != "calculated") return (null, $"当前状态 {run.Status} 不能审批发薪");

        run.Status = "paid";
        run.PaidAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // 推 FIN：薪资凭证（借 应付职工薪酬 / 贷 银行存款）
        FireFinExpenseWebhook(run);
        return (run, null);
    }

    private void FireFinExpenseWebhook(HrmPayrollRun run)
    {
        var baseUrl = config["Webhooks:FinBaseUrl"];
        if (string.IsNullOrEmpty(baseUrl)) return;
        var secret = config["Webhooks:FinSecret"] ?? "";

        _ = Task.Run(async () =>
        {
            try
            {
                var payload = new
                {
                    OaApprovalId = run.PayrollRunId * -1, // 用负值区分非 OA 来源
                    Amount = run.TotalNetPay,
                    Summary = $"{run.PeriodCode} 月度薪资发放（{run.EmployeeCount} 人）",
                };
                var json = JsonSerializer.Serialize(payload);
                using var client = httpFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(5);
                using var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/webhook/oa/expense-approved")
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json"),
                };
                if (!string.IsNullOrEmpty(secret))
                {
                    var sig = ComputeHmac(json, secret);
                    req.Headers.Add("X-Fin-Signature", $"sha256={sig}");
                }
                await client.SendAsync(req);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "FIN 薪资 webhook 推送失败 — 可后续手工补凭证");
            }
        });
    }

    private static string ComputeHmac(string body, string secret)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        var data = Encoding.UTF8.GetBytes(body);
        return Convert.ToHexString(System.Security.Cryptography.HMACSHA256.HashData(key, data)).ToLowerInvariant();
    }
}
