namespace OctopusFIN.Api.Persistence;

public static class FinDbSeeder
{
    public static void Seed(FinDbContext db)
    {
        SeedBaseIfEmpty(db);
        SeedSimulatedBusiness(db); // 幂等：已有 SIM- 凭证则跳过
        db.SaveChanges();
    }

    // ============ 基础数据（仅首次建库）============
    private static void SeedBaseIfEmpty(FinDbContext db)
    {
        if (db.Accounts.Any()) return;

        var now = DateTime.UtcNow;

        // 标准 COA 科目表（精简版 — 一级 5 类 + 二级常用科目）
        db.Accounts.AddRange(
            new FinAccount { AccountId = 1, AccountCode = "1001", AccountName = "库存现金",     AccountType = "asset",     Direction = "debit",  Level = 1, IsLeaf = true, Status = "active", CreatedAt = now },
            new FinAccount { AccountId = 2, AccountCode = "1002", AccountName = "银行存款",     AccountType = "asset",     Direction = "debit",  Level = 1, IsLeaf = true, Status = "active", CreatedAt = now },
            new FinAccount { AccountId = 3, AccountCode = "1122", AccountName = "应收账款",     AccountType = "asset",     Direction = "debit",  Level = 1, IsLeaf = true, Status = "active", CreatedAt = now },
            new FinAccount { AccountId = 4, AccountCode = "1405", AccountName = "库存商品",     AccountType = "asset",     Direction = "debit",  Level = 1, IsLeaf = true, Status = "active", CreatedAt = now },
            new FinAccount { AccountId = 5, AccountCode = "1601", AccountName = "固定资产",     AccountType = "asset",     Direction = "debit",  Level = 1, IsLeaf = true, Status = "active", CreatedAt = now },
            new FinAccount { AccountId = 6, AccountCode = "2202", AccountName = "应付账款",     AccountType = "liability", Direction = "credit", Level = 1, IsLeaf = true, Status = "active", CreatedAt = now },
            new FinAccount { AccountId = 7, AccountCode = "2211", AccountName = "应付职工薪酬", AccountType = "liability", Direction = "credit", Level = 1, IsLeaf = true, Status = "active", CreatedAt = now },
            new FinAccount { AccountId = 8, AccountCode = "2221", AccountName = "应交税费",     AccountType = "liability", Direction = "credit", Level = 1, IsLeaf = true, Status = "active", CreatedAt = now },
            new FinAccount { AccountId = 9, AccountCode = "4001", AccountName = "实收资本",     AccountType = "equity",    Direction = "credit", Level = 1, IsLeaf = true, Status = "active", CreatedAt = now },
            new FinAccount { AccountId = 10, AccountCode = "4103", AccountName = "本年利润",    AccountType = "equity",    Direction = "credit", Level = 1, IsLeaf = true, Status = "active", CreatedAt = now },
            new FinAccount { AccountId = 11, AccountCode = "6001", AccountName = "主营业务收入", AccountType = "revenue",   Direction = "credit", Level = 1, IsLeaf = true, Status = "active", CreatedAt = now },
            new FinAccount { AccountId = 12, AccountCode = "6051", AccountName = "其他业务收入", AccountType = "revenue",   Direction = "credit", Level = 1, IsLeaf = true, Status = "active", CreatedAt = now },
            new FinAccount { AccountId = 13, AccountCode = "6401", AccountName = "主营业务成本", AccountType = "expense",   Direction = "debit",  Level = 1, IsLeaf = true, Status = "active", CreatedAt = now },
            new FinAccount { AccountId = 14, AccountCode = "6601", AccountName = "销售费用",     AccountType = "expense",   Direction = "debit",  Level = 1, IsLeaf = true, Status = "active", CreatedAt = now },
            new FinAccount { AccountId = 15, AccountCode = "6602", AccountName = "管理费用",     AccountType = "expense",   Direction = "debit",  Level = 1, IsLeaf = true, Status = "active", CreatedAt = now },
            new FinAccount { AccountId = 16, AccountCode = "6603", AccountName = "财务费用",     AccountType = "expense",   Direction = "debit",  Level = 1, IsLeaf = true, Status = "active", CreatedAt = now }
        );

        var thisYear = now.Year;
        var thisMonth = now.Month;
        var prevDate = now.AddMonths(-1);
        db.Periods.AddRange(
            new FinPeriod { PeriodId = 1, PeriodCode = $"{prevDate.Year}-{prevDate.Month:D2}", Year = prevDate.Year, Month = prevDate.Month, Status = "closed", ClosedAt = now.AddDays(-3), ClosedBy = 1 },
            new FinPeriod { PeriodId = 2, PeriodCode = $"{thisYear}-{thisMonth:D2}", Year = thisYear, Month = thisMonth, Status = "open" }
        );

        db.SyncUsers.AddRange(
            new FinSyncUser { Id = 1, UmcUserId = 1, UserName = "admin", NickName = "超级管理员", Email = "admin@octopus.com", LastSyncAt = now },
            new FinSyncUser { Id = 2, UmcUserId = 2, UserName = "zhangsan", NickName = "张三", Email = "zhangsan@octopus.com", LastSyncAt = now },
            new FinSyncUser { Id = 3, UmcUserId = 3, UserName = "lisi", NickName = "李四", Email = "lisi@octopus.com", LastSyncAt = now }
        );

        db.Organizations.AddRange(
            new FinOrganization { OrganizationId = 1, OrgCode = "HQ", OrgName = "总部", UmcDeptId = 1, Status = "active", IsPrimary = true, CreatedAt = now },
            new FinOrganization { OrganizationId = 2, OrgCode = "SOUTH", OrgName = "华南分公司", UmcDeptId = 10, Status = "active", IsPrimary = false, CreatedAt = now }
        );

        db.ProfitCenters.AddRange(
            new FinProfitCenter { ProfitCenterId = 1, CenterCode = "PC-SALES", CenterName = "销售部", OrganizationId = 1, Status = "active", CreatedAt = now },
            new FinProfitCenter { ProfitCenterId = 2, CenterCode = "PC-PROD", CenterName = "生产部", OrganizationId = 1, Status = "active", CreatedAt = now },
            new FinProfitCenter { ProfitCenterId = 3, CenterCode = "PC-RND", CenterName = "研发部", OrganizationId = 1, Status = "active", CreatedAt = now }
        );

        // 期初凭证：实收资本 100 万
        var openingVoucher = new FinVoucher
        {
            VoucherId = 1,
            VoucherNo = $"V-{prevDate:yyyyMM}-0001",
            PeriodId = 1,
            VoucherDate = new DateTime(prevDate.Year, prevDate.Month, 1, 0, 0, 0, DateTimeKind.Utc),
            VoucherType = "opening",
            Summary = "实收资本（开账）",
            TotalDebit = 1000000m,
            TotalCredit = 1000000m,
            Status = "posted",
            Source = "manual",
            CreatedBy = 1,
            CreatedAt = now.AddDays(-30),
            PostedAt = now.AddDays(-30),
        };
        db.Vouchers.Add(openingVoucher);
        db.SaveChanges();

        db.VoucherEntries.AddRange(
            new FinVoucherEntry { VoucherId = openingVoucher.VoucherId, LineOrder = 1, AccountId = 2, AccountCode = "1002", AccountName = "银行存款", Summary = "开账", DebitAmount = 1000000m, CreditAmount = 0m },
            new FinVoucherEntry { VoucherId = openingVoucher.VoucherId, LineOrder = 2, AccountId = 9, AccountCode = "4001", AccountName = "实收资本", Summary = "开账", DebitAmount = 0m, CreditAmount = 1000000m }
        );

        db.SaveChanges();
    }

    // ============ 模拟业务数据（幂等，供财务报表演示）============
    public static void SeedSimulatedBusiness(FinDbContext db)
    {
        if (db.Vouchers.Any(v => v.VoucherNo.StartsWith("SIM-"))) return;
        if (!db.Accounts.Any()) return;

        // 确保组织/利润中心存在（兼容旧库）
        if (!db.Organizations.Any())
        {
            db.Organizations.AddRange(
                new FinOrganization { OrgCode = "HQ", OrgName = "总部", Status = "active", IsPrimary = true, CreatedAt = DateTime.UtcNow },
                new FinOrganization { OrgCode = "SOUTH", OrgName = "华南分公司", Status = "active", IsPrimary = false, CreatedAt = DateTime.UtcNow });
            db.SaveChanges();
        }
        if (!db.ProfitCenters.Any())
        {
            var hqId = db.Organizations.First(o => o.OrgCode == "HQ").OrganizationId;
            db.ProfitCenters.AddRange(
                new FinProfitCenter { CenterCode = "PC-SALES", CenterName = "销售部", OrganizationId = hqId, Status = "active", CreatedAt = DateTime.UtcNow },
                new FinProfitCenter { CenterCode = "PC-PROD", CenterName = "生产部", OrganizationId = hqId, Status = "active", CreatedAt = DateTime.UtcNow },
                new FinProfitCenter { CenterCode = "PC-RND", CenterName = "研发部", OrganizationId = hqId, Status = "active", CreatedAt = DateTime.UtcNow });
            db.SaveChanges();
        }

        var periods = db.Periods.OrderBy(p => p.PeriodId).ToList();
        if (periods.Count == 0) return;
        var pPrev = periods[0];
        var pCur = periods.FirstOrDefault(p => p.Status == "open") ?? periods[^1];

        var accounts = db.Accounts.ToDictionary(a => a.AccountCode);
        FinAccount A(string code) => accounts[code];

        long hq = db.Organizations.First(o => o.OrgCode == "HQ").OrganizationId;
        long south = db.Organizations.First(o => o.OrgCode == "SOUTH").OrganizationId;
        long pcSales = db.ProfitCenters.First(p => p.CenterCode == "PC-SALES").ProfitCenterId;
        long pcProd = db.ProfitCenters.First(p => p.CenterCode == "PC-PROD").ProfitCenterId;
        long pcRnd = db.ProfitCenters.First(p => p.CenterCode == "PC-RND").ProfitCenterId;

        var now = DateTime.UtcNow;
        int seq = 0;

        void AddVoucher(long periodId, DateTime date, long orgId, string summary,
            params (string code, decimal debit, decimal credit, long? pc)[] lines)
        {
            seq++;
            var v = new FinVoucher
            {
                VoucherNo = $"SIM-{seq:D4}",
                PeriodId = periodId,
                VoucherDate = date,
                VoucherType = "normal",
                Summary = summary,
                TotalDebit = lines.Sum(l => l.debit),
                TotalCredit = lines.Sum(l => l.credit),
                Status = "posted",
                Source = "manual",
                OrganizationId = orgId,
                CreatedBy = 1,
                CreatedAt = now,
                PostedAt = now,
            };
            db.Vouchers.Add(v);
            db.SaveChanges();

            int line = 0;
            foreach (var l in lines)
            {
                var acct = A(l.code);
                db.VoucherEntries.Add(new FinVoucherEntry
                {
                    VoucherId = v.VoucherId,
                    LineOrder = ++line,
                    AccountId = acct.AccountId,
                    AccountCode = acct.AccountCode,
                    AccountName = acct.AccountName,
                    Summary = summary,
                    DebitAmount = l.debit,
                    CreditAmount = l.credit,
                    Currency = "CNY",
                    ProfitCenterId = l.pc,
                });
            }
            db.SaveChanges();
        }

        // ===== 上月业务（已关账期）=====
        var prevDate = new DateTime(pPrev.Year, pPrev.Month, 15, 0, 0, 0, DateTimeKind.Utc);
        AddVoucher(pPrev.PeriodId, prevDate, hq, "赊购原材料入库",
            ("1405", 300000m, 0m, pcProd), ("2202", 0m, 300000m, pcProd));
        AddVoucher(pPrev.PeriodId, prevDate, hq, "确认销售收入",
            ("1122", 500000m, 0m, pcSales), ("6001", 0m, 500000m, pcSales));
        AddVoucher(pPrev.PeriodId, prevDate, hq, "结转销售成本",
            ("6401", 300000m, 0m, pcProd), ("1405", 0m, 300000m, pcProd));
        AddVoucher(pPrev.PeriodId, prevDate, hq, "支付管理费用",
            ("6602", 50000m, 0m, pcRnd), ("1002", 0m, 50000m, pcRnd));
        AddVoucher(pPrev.PeriodId, prevDate, hq, "计提职工薪酬",
            ("6602", 80000m, 0m, pcProd), ("2211", 0m, 80000m, pcProd));
        AddVoucher(pPrev.PeriodId, prevDate, hq, "提取备用金",
            ("1001", 20000m, 0m, (long?)null), ("1002", 0m, 20000m, null));

        // ===== 本月业务（开放期）=====
        var curDate = new DateTime(pCur.Year, pCur.Month, 10, 0, 0, 0, DateTimeKind.Utc);
        AddVoucher(pCur.PeriodId, curDate, hq, "赊购商品入库",
            ("1405", 400000m, 0m, pcProd), ("2202", 0m, 400000m, pcProd));
        AddVoucher(pCur.PeriodId, curDate, hq, "确认销售收入",
            ("1122", 600000m, 0m, pcSales), ("6001", 0m, 600000m, pcSales));
        AddVoucher(pCur.PeriodId, curDate, south, "其他业务收入（分公司）",
            ("1122", 100000m, 0m, pcSales), ("6051", 0m, 100000m, pcSales));
        AddVoucher(pCur.PeriodId, curDate, hq, "结转销售成本",
            ("6401", 350000m, 0m, pcProd), ("1405", 0m, 350000m, pcProd));
        AddVoucher(pCur.PeriodId, curDate, hq, "支付销售费用",
            ("6601", 60000m, 0m, pcSales), ("1002", 0m, 60000m, pcSales));
        AddVoucher(pCur.PeriodId, curDate, hq, "支付管理费用",
            ("6602", 90000m, 0m, pcRnd), ("1002", 0m, 90000m, pcRnd));
        AddVoucher(pCur.PeriodId, curDate, hq, "支付财务费用",
            ("6603", 10000m, 0m, pcProd), ("1002", 0m, 10000m, pcProd));
        AddVoucher(pCur.PeriodId, curDate, hq, "收回应收账款",
            ("1002", 400000m, 0m, pcSales), ("1122", 0m, 400000m, pcSales));
        AddVoucher(pCur.PeriodId, curDate, hq, "支付应付账款",
            ("2202", 250000m, 0m, pcProd), ("1002", 0m, 250000m, pcProd));

        db.SaveChanges();
    }
}