namespace OctopusFIN.Api.Persistence;

public static class FinDbSeeder
{
    public static void Seed(FinDbContext db)
    {
        if (db.Accounts.Any()) return;

        var now = DateTime.UtcNow;

        // 标准 COA 科目表（精简版 — 一级 5 类 + 二级常用科目）
        db.Accounts.AddRange(
            // 资产类（1xxx，借方）
            new FinAccount { AccountId = 1, AccountCode = "1001", AccountName = "库存现金",     AccountType = "asset",     Direction = "debit",  Level = 1, IsLeaf = true, Status = "active", CreatedAt = now },
            new FinAccount { AccountId = 2, AccountCode = "1002", AccountName = "银行存款",     AccountType = "asset",     Direction = "debit",  Level = 1, IsLeaf = true, Status = "active", CreatedAt = now },
            new FinAccount { AccountId = 3, AccountCode = "1122", AccountName = "应收账款",     AccountType = "asset",     Direction = "debit",  Level = 1, IsLeaf = true, Status = "active", CreatedAt = now },
            new FinAccount { AccountId = 4, AccountCode = "1405", AccountName = "库存商品",     AccountType = "asset",     Direction = "debit",  Level = 1, IsLeaf = true, Status = "active", CreatedAt = now },
            new FinAccount { AccountId = 5, AccountCode = "1601", AccountName = "固定资产",     AccountType = "asset",     Direction = "debit",  Level = 1, IsLeaf = true, Status = "active", CreatedAt = now },

            // 负债类（2xxx，贷方）
            new FinAccount { AccountId = 6, AccountCode = "2202", AccountName = "应付账款",     AccountType = "liability", Direction = "credit", Level = 1, IsLeaf = true, Status = "active", CreatedAt = now },
            new FinAccount { AccountId = 7, AccountCode = "2211", AccountName = "应付职工薪酬", AccountType = "liability", Direction = "credit", Level = 1, IsLeaf = true, Status = "active", CreatedAt = now },
            new FinAccount { AccountId = 8, AccountCode = "2221", AccountName = "应交税费",     AccountType = "liability", Direction = "credit", Level = 1, IsLeaf = true, Status = "active", CreatedAt = now },

            // 所有者权益（3xxx，贷方）
            new FinAccount { AccountId = 9, AccountCode = "4001", AccountName = "实收资本",     AccountType = "equity",    Direction = "credit", Level = 1, IsLeaf = true, Status = "active", CreatedAt = now },
            new FinAccount { AccountId = 10, AccountCode = "4103", AccountName = "本年利润",    AccountType = "equity",    Direction = "credit", Level = 1, IsLeaf = true, Status = "active", CreatedAt = now },

            // 收入类（5xxx/6xxx，贷方）
            new FinAccount { AccountId = 11, AccountCode = "6001", AccountName = "主营业务收入", AccountType = "revenue",   Direction = "credit", Level = 1, IsLeaf = true, Status = "active", CreatedAt = now },
            new FinAccount { AccountId = 12, AccountCode = "6051", AccountName = "其他业务收入", AccountType = "revenue",   Direction = "credit", Level = 1, IsLeaf = true, Status = "active", CreatedAt = now },

            // 费用类（6xxx，借方）
            new FinAccount { AccountId = 13, AccountCode = "6401", AccountName = "主营业务成本", AccountType = "expense",   Direction = "debit",  Level = 1, IsLeaf = true, Status = "active", CreatedAt = now },
            new FinAccount { AccountId = 14, AccountCode = "6601", AccountName = "销售费用",     AccountType = "expense",   Direction = "debit",  Level = 1, IsLeaf = true, Status = "active", CreatedAt = now },
            new FinAccount { AccountId = 15, AccountCode = "6602", AccountName = "管理费用",     AccountType = "expense",   Direction = "debit",  Level = 1, IsLeaf = true, Status = "active", CreatedAt = now },
            new FinAccount { AccountId = 16, AccountCode = "6603", AccountName = "财务费用",     AccountType = "expense",   Direction = "debit",  Level = 1, IsLeaf = true, Status = "active", CreatedAt = now }
        );

        // 当前会计期间 + 上月（已关账）
        var thisYear = now.Year;
        var thisMonth = now.Month;
        var prevDate = now.AddMonths(-1);
        db.Periods.AddRange(
            new FinPeriod { PeriodId = 1, PeriodCode = $"{prevDate.Year}-{prevDate.Month:D2}", Year = prevDate.Year, Month = prevDate.Month, Status = "closed", ClosedAt = now.AddDays(-3), ClosedBy = 1 },
            new FinPeriod { PeriodId = 2, PeriodCode = $"{thisYear}-{thisMonth:D2}", Year = thisYear, Month = thisMonth, Status = "open" }
        );

        // 同步用户
        db.SyncUsers.AddRange(
            new FinSyncUser { Id = 1, UmcUserId = 1, UserName = "admin", NickName = "超级管理员", Email = "admin@octopus.com", LastSyncAt = now },
            new FinSyncUser { Id = 2, UmcUserId = 2, UserName = "zhangsan", NickName = "张三", Email = "zhangsan@octopus.com", LastSyncAt = now },
            new FinSyncUser { Id = 3, UmcUserId = 3, UserName = "lisi", NickName = "李四", Email = "lisi@octopus.com", LastSyncAt = now }
        );

        // 一条期初凭证：实收资本 100 万
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
}
