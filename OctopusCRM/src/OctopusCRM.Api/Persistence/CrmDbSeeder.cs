namespace OctopusCRM.Api.Persistence;

public static class CrmDbSeeder
{
    public static void Seed(CrmDbContext db)
    {
        if (db.SyncUsers.Any()) return;

        var now = DateTime.UtcNow;

        // 同步用户（对应 UMC UserId 1/2/3）
        db.SyncUsers.AddRange(
            new CrmSyncUser
            {
                UmcUserId = 1, UserName = "admin", NickName = "超级管理员",
                Email = "admin@octopus.com", Status = 1, LastSyncAt = now
            },
            new CrmSyncUser
            {
                UmcUserId = 2, UserName = "zhangsan", NickName = "张三",
                Email = "zhangsan@octopus.com", PhoneNumber = "13800138001", Status = 1, LastSyncAt = now
            },
            new CrmSyncUser
            {
                UmcUserId = 3, UserName = "lisi", NickName = "李四",
                Email = "lisi@octopus.com", PhoneNumber = "13800138002", Status = 1, LastSyncAt = now
            }
        );

        // 客户
        var customerHuawei = new CrmCustomer
        {
            CustomerCode = "CUS-20260101-001",
            CustomerName = "华为技术有限公司",
            IndustryType = "电子",
            Level = "A",
            Status = "active",
            Address = "广东省深圳市龙岗区坂田华为基地",
            Website = "https://www.huawei.com",
            Remark = "重点客户，优先服务",
            CreatedBy = 1,
            CreatedAt = now.AddDays(-60),
            UpdatedAt = now.AddDays(-5)
        };

        var customerAlibaba = new CrmCustomer
        {
            CustomerCode = "CUS-20260101-002",
            CustomerName = "阿里巴巴集团控股有限公司",
            IndustryType = "互联网",
            Level = "B",
            Status = "active",
            Address = "浙江省杭州市余杭区五常街道",
            Website = "https://www.alibaba.com",
            Remark = "互联网头部企业",
            CreatedBy = 2,
            CreatedAt = now.AddDays(-45),
            UpdatedAt = now.AddDays(-10)
        };

        var customerByteDance = new CrmCustomer
        {
            CustomerCode = "CUS-20260101-003",
            CustomerName = "字节跳动有限公司",
            IndustryType = "互联网",
            Level = "C",
            Status = "prospect",
            Address = "北京市海淀区知春路抖音大厦",
            Website = "https://www.bytedance.com",
            Remark = "潜在客户，持续跟进",
            CreatedBy = 2,
            CreatedAt = now.AddDays(-20),
            UpdatedAt = now.AddDays(-3)
        };

        // 新增客户：与 PLM 商品直接关联的分销商
        var customerSuning = new CrmCustomer
        {
            CustomerCode = "CUS-20260101-004",
            CustomerName = "苏宁易购集团股份有限公司",
            IndustryType = "零售",
            Level = "A",
            Status = "active",
            Address = "江苏省南京市玄武区苏宁总部",
            Website = "https://www.suning.com",
            Remark = "全国连锁零售商，主要采购智能手机分销",
            CreatedBy = 2,
            CreatedAt = now.AddDays(-30),
            UpdatedAt = now.AddDays(-2)
        };

        var customerJD = new CrmCustomer
        {
            CustomerCode = "CUS-20260101-005",
            CustomerName = "京东科技控股股份有限公司",
            IndustryType = "零售",
            Level = "B",
            Status = "prospect",
            Address = "北京市朝阳区京东大厦",
            Website = "https://www.jd.com",
            Remark = "正在洽谈年度框架合同",
            CreatedBy = 3,
            CreatedAt = now.AddDays(-10),
            UpdatedAt = now.AddDays(-1)
        };

        db.Customers.AddRange(customerHuawei, customerAlibaba, customerByteDance, customerSuning, customerJD);
        db.SaveChanges();

        // 联系人
        db.Contacts.AddRange(
            new CrmContact
            {
                CustomerId = customerHuawei.CustomerId,
                Name = "王采购",
                Title = "采购总监",
                Phone = "13900139001",
                Email = "wang.caigou@huawei.com",
                IsPrimary = true,
                CreatedAt = now.AddDays(-60)
            },
            new CrmContact
            {
                CustomerId = customerHuawei.CustomerId,
                Name = "李技术",
                Title = "技术经理",
                Phone = "13900139002",
                Email = "li.jishu@huawei.com",
                IsPrimary = false,
                CreatedAt = now.AddDays(-58)
            },
            new CrmContact
            {
                CustomerId = customerAlibaba.CustomerId,
                Name = "赵采购",
                Title = "采购经理",
                Phone = "13900139003",
                Email = "zhao.caigou@alibaba.com",
                IsPrimary = true,
                CreatedAt = now.AddDays(-45)
            },
            new CrmContact
            {
                CustomerId = customerByteDance.CustomerId,
                Name = "钱总",
                Title = "硬件负责人",
                Phone = "13900139004",
                Email = "qian@bytedance.com",
                IsPrimary = true,
                CreatedAt = now.AddDays(-20)
            },
            new CrmContact
            {
                CustomerId = customerSuning.CustomerId,
                Name = "孙采购",
                Title = "手机品类采购总监",
                Phone = "13900139005",
                Email = "sun.caigou@suning.com",
                IsPrimary = true,
                CreatedAt = now.AddDays(-30)
            },
            new CrmContact
            {
                CustomerId = customerJD.CustomerId,
                Name = "周经理",
                Title = "供应链负责人",
                Phone = "13900139006",
                Email = "zhou@jd.com",
                IsPrimary = true,
                CreatedAt = now.AddDays(-10)
            }
        );

        // 询盘
        var inquiryHuawei = new CrmInquiry
        {
            InquiryCode = "INQ-20260101-001",
            CustomerId = customerHuawei.CustomerId,
            Title = "华为手机屏幕及相机模组采购询盘",
            Description = "需要采购新款旗舰手机屏幕和相机模组，数量较大",
            Status = "quoted",
            ExpectedDelivery = now.AddDays(30),
            AssignedTo = 2,        // UMC UserId=2 张三
            AssignedToName = "张三",
            CreatedBy = 2,
            CreatedAt = now.AddDays(-40),
            UpdatedAt = now.AddDays(-20)
        };

        var inquiryAlibaba = new CrmInquiry
        {
            InquiryCode = "INQ-20260101-002",
            CustomerId = customerAlibaba.CustomerId,
            Title = "阿里巴巴服务器配件采购询盘",
            Description = "数据中心扩容，需要采购一批服务器配件",
            Status = "open",
            ExpectedDelivery = now.AddDays(45),
            AssignedTo = 3,        // UMC UserId=3 李四
            AssignedToName = "李四",
            CreatedBy = 3,
            CreatedAt = now.AddDays(-15),
            UpdatedAt = now.AddDays(-15)
        };

        // 苏宁询盘（关联 PLM 商品 iPhone 15 Pro + 小米 14）
        var inquirySuning = new CrmInquiry
        {
            InquiryCode = "INQ-20260101-003",
            CustomerId = customerSuning.CustomerId,
            Title = "苏宁易购 Q2 智能手机采购询盘",
            Description = "面向 Q2 旺季备货，需要大批量采购主流智能手机机型",
            Status = "in_progress",
            ExpectedDelivery = now.AddDays(20),
            AssignedTo = 2,        // UMC UserId=2 张三
            AssignedToName = "张三",
            CreatedBy = 2,
            CreatedAt = now.AddDays(-8),
            UpdatedAt = now.AddDays(-2)
        };

        // 京东询盘（关联 PLM 小米 14）
        var inquiryJD = new CrmInquiry
        {
            InquiryCode = "INQ-20260101-004",
            CustomerId = customerJD.CustomerId,
            Title = "京东商城手机品类试销询盘",
            Description = "先小批量试销，测试销量后决定年度框架采购量",
            Status = "open",
            ExpectedDelivery = now.AddDays(60),
            AssignedTo = 3,        // UMC UserId=3 李四
            AssignedToName = "李四",
            CreatedBy = 3,
            CreatedAt = now.AddDays(-3),
            UpdatedAt = now.AddDays(-3)
        };

        db.Inquiries.AddRange(inquiryHuawei, inquiryAlibaba, inquirySuning, inquiryJD);
        db.SaveChanges();

        // 询盘明细
        db.InquiryItems.AddRange(
            // 华为询盘：屏幕+摄像头组件
            new CrmInquiryItem
            {
                InquiryId = inquiryHuawei.InquiryId,
                ProductName = "OLED手机屏幕",
                Spec = "6.7英寸 2K 120Hz",
                Quantity = 10000,
                Unit = "块",
                Remark = "需要通过华为认证"
            },
            new CrmInquiryItem
            {
                InquiryId = inquiryHuawei.InquiryId,
                ProductName = "摄像头模组",
                Spec = "5000万像素 潜望式变焦",
                Quantity = 10000,
                Unit = "套",
                Remark = "含支架和连接线"
            },
            // 阿里巴巴询盘：服务器配件
            new CrmInquiryItem
            {
                InquiryId = inquiryAlibaba.InquiryId,
                ProductName = "服务器内存条",
                Spec = "DDR5 64GB ECC",
                Quantity = 500,
                Unit = "条",
                Remark = null
            },
            new CrmInquiryItem
            {
                InquiryId = inquiryAlibaba.InquiryId,
                ProductName = "NVMe固态硬盘",
                Spec = "PCIe 4.0 4TB",
                Quantity = 200,
                Unit = "块",
                Remark = "企业级"
            },
            // 苏宁询盘：关联 PLM 商品（PLM ProductId = 1: iPhone 15 Pro, 2: 小米 14）
            new CrmInquiryItem
            {
                InquiryId = inquirySuning.InquiryId,
                ProductName = "iPhone 15 Pro",
                Spec = "256GB 钛金属原色",
                Quantity = 2000,
                Unit = "台",
                PlmProductId = 1,   // PLM: iPhone 15 Pro
                Remark = "需附带保修卡和配件"
            },
            new CrmInquiryItem
            {
                InquiryId = inquirySuning.InquiryId,
                ProductName = "小米 14",
                Spec = "512GB 岩石青",
                Quantity = 3000,
                Unit = "台",
                PlmProductId = 2,   // PLM: 小米 14
                Remark = "附赠原装充电套装"
            },
            // 京东询盘：关联 PLM 小米 14
            new CrmInquiryItem
            {
                InquiryId = inquiryJD.InquiryId,
                ProductName = "小米 14",
                Spec = "256GB 黑色",
                Quantity = 500,
                Unit = "台",
                PlmProductId = 2,   // PLM: 小米 14
                Remark = "试销批次"
            }
        );

        // 报价单（华为）
        var quoteHuawei = new CrmQuote
        {
            QuoteCode = "QUO-20260101-001",
            InquiryId = inquiryHuawei.InquiryId,
            CustomerId = customerHuawei.CustomerId,
            Status = "approved",
            TotalAmount = 280000m,
            Currency = "CNY",
            ValidUntil = now.AddDays(30),
            ExpectedDelivery = now.AddDays(30),
            Terms = "货到付款30%，发货前付清70%",
            Remark = "已获客户口头确认",
            OaApprovalId = 1,      // OA 审批 ID=1（张三的请假申请——演示数据中复用）
            CreatedBy = 2,
            CreatedAt = now.AddDays(-20),
            UpdatedAt = now.AddDays(-10)
        };

        // 报价单（苏宁，关联 PLM 商品）
        var quoteSuning = new CrmQuote
        {
            QuoteCode = "QUO-20260101-002",
            InquiryId = inquirySuning.InquiryId,
            CustomerId = customerSuning.CustomerId,
            Status = "submitted",
            TotalAmount = 36800000m,   // iPhone 15 Pro × 2000 @ 9999 + 小米 14 × 3000 @ 3999 ≈ 3198w+1200w
            Currency = "CNY",
            ValidUntil = now.AddDays(15),
            ExpectedDelivery = now.AddDays(20),
            Terms = "款到发货，分两批交付",
            Remark = "苏宁 Q2 备货报价",
            OaApprovalId = 2,      // OA 审批 ID=2（李四的报销申请——演示数据中复用）
            CreatedBy = 2,
            CreatedAt = now.AddDays(-3),
            UpdatedAt = now.AddDays(-1)
        };

        db.Quotes.AddRange(quoteHuawei, quoteSuning);
        db.SaveChanges();

        // 报价明细
        db.QuoteItems.AddRange(
            // 华为报价明细
            new CrmQuoteItem
            {
                QuoteId = quoteHuawei.QuoteId,
                ProductName = "OLED手机屏幕",
                Spec = "6.7英寸 2K 120Hz",
                Quantity = 10000,
                Unit = "块",
                UnitPrice = 18m,
                Amount = 180000m,
                Remark = null
            },
            new CrmQuoteItem
            {
                QuoteId = quoteHuawei.QuoteId,
                ProductName = "摄像头模组",
                Spec = "5000万像素 潜望式变焦",
                Quantity = 10000,
                Unit = "套",
                UnitPrice = 10m,
                Amount = 100000m,
                Remark = null
            },
            // 苏宁报价明细（关联 PLM 商品）
            new CrmQuoteItem
            {
                QuoteId = quoteSuning.QuoteId,
                ProductName = "iPhone 15 Pro",
                Spec = "256GB 钛金属原色",
                Quantity = 2000,
                Unit = "台",
                UnitPrice = 9999m,
                Amount = 19998000m,
                PlmProductId = 1,   // PLM: iPhone 15 Pro
                Remark = null
            },
            new CrmQuoteItem
            {
                QuoteId = quoteSuning.QuoteId,
                ProductName = "小米 14",
                Spec = "512GB 岩石青",
                Quantity = 3000,
                Unit = "台",
                UnitPrice = 3999m,
                Amount = 11997000m,
                PlmProductId = 2,   // PLM: 小米 14
                Remark = null
            }
        );

        // 合同（华为）
        var contractHuawei = new CrmContract
        {
            ContractCode = "CON-20260101-001",
            QuoteId = quoteHuawei.QuoteId,
            CustomerId = customerHuawei.CustomerId,
            Title = "华为手机屏幕及相机模组采购合同",
            TotalAmount = 280000m,
            Currency = "CNY",
            SignDate = now.AddDays(-10),
            DeliveryDate = now.AddDays(30),
            Status = "active",
            OaApprovalId = 2,      // OA 审批 ID=2
            Remark = "重点合同，按时交付",
            CreatedBy = 2,
            CreatedAt = now.AddDays(-10),
            UpdatedAt = now.AddDays(-10)
        };

        db.Contracts.Add(contractHuawei);
        db.SaveChanges();

        // 合同明细
        db.ContractItems.AddRange(
            new CrmContractItem
            {
                ContractId = contractHuawei.ContractId,
                ProductName = "OLED手机屏幕",
                Quantity = 10000,
                Unit = "块",
                UnitPrice = 18m,
                Amount = 180000m
            },
            new CrmContractItem
            {
                ContractId = contractHuawei.ContractId,
                ProductName = "摄像头模组",
                Quantity = 10000,
                Unit = "套",
                UnitPrice = 10m,
                Amount = 100000m
            }
        );

        // 回款（30% 预付款 = 84000）
        db.Payments.Add(new CrmPayment
        {
            ContractId = contractHuawei.ContractId,
            Amount = 84000m,
            PaymentMethod = "银行转账",
            PaymentDate = now.AddDays(-8),
            Status = "confirmed",
            BankReference = "TXN20260101001",
            Remark = "合同预付款30%",
            CreatedBy = 2,
            CreatedAt = now.AddDays(-8)
        });

        db.SaveChanges();

        // ─── 历史订单（BI 看板演示数据：5 个月 OTD 趋势 + 审批积压 + DSO）───────────
        // 每月 15 日作为发货锚点，保证 OTD 趋势图落入正确月份
        var m5 = new DateTime(now.Year, now.Month, 15, 0, 0, 0, DateTimeKind.Utc).AddMonths(-5);
        var m4 = new DateTime(now.Year, now.Month, 15, 0, 0, 0, DateTimeKind.Utc).AddMonths(-4);
        var m3 = new DateTime(now.Year, now.Month, 15, 0, 0, 0, DateTimeKind.Utc).AddMonths(-3);
        var m2 = new DateTime(now.Year, now.Month, 15, 0, 0, 0, DateTimeKind.Utc).AddMonths(-2);
        var m1 = new DateTime(now.Year, now.Month, 15, 0, 0, 0, DateTimeKind.Utc).AddMonths(-1);

        // ── 历史询盘（5 条）──────────────────────────────────────────────────────────
        var inqH1 = new CrmInquiry
        {
            InquiryCode = "INQ-HIST-001", CustomerId = customerSuning.CustomerId,
            Title = "苏宁 iPhone 春季大批量备货询盘", Status = "won",
            AssignedTo = 2, AssignedToName = "张三", CreatedBy = 2,
            CreatedAt = m5.AddDays(-22), UpdatedAt = m5.AddDays(-10)
        };
        var inqH2 = new CrmInquiry
        {
            InquiryCode = "INQ-HIST-002", CustomerId = customerHuawei.CustomerId,
            Title = "华为手机维修配件年度采购询盘", Status = "won",
            AssignedTo = 2, AssignedToName = "张三", CreatedBy = 2,
            CreatedAt = m4.AddDays(-16), UpdatedAt = m4.AddDays(-5)
        };
        var inqH3 = new CrmInquiry
        {
            InquiryCode = "INQ-HIST-003", CustomerId = customerAlibaba.CustomerId,
            Title = "阿里巴巴数据中心服务器扩容硬件询盘", Status = "won",
            AssignedTo = 3, AssignedToName = "李四", CreatedBy = 3,
            CreatedAt = m3.AddDays(-20), UpdatedAt = m3.AddDays(-8)
        };
        var inqH4 = new CrmInquiry
        {
            InquiryCode = "INQ-HIST-004", CustomerId = customerByteDance.CustomerId,
            Title = "字节跳动服务器硬件试购询盘", Status = "won",
            AssignedTo = 3, AssignedToName = "李四", CreatedBy = 3,
            CreatedAt = m2.AddDays(-16), UpdatedAt = m2.AddDays(-5)
        };
        var inqH5 = new CrmInquiry
        {
            InquiryCode = "INQ-HIST-005", CustomerId = customerSuning.CustomerId,
            Title = "苏宁小米 14 Q1 备货询盘", Status = "won",
            AssignedTo = 2, AssignedToName = "张三", CreatedBy = 2,
            CreatedAt = m1.AddDays(-11), UpdatedAt = m1.AddDays(-3)
        };
        db.Inquiries.AddRange(inqH1, inqH2, inqH3, inqH4, inqH5);
        db.SaveChanges();

        // ── 历史报价（5 条已批准 + 1 条审批中积压）───────────────────────────────────
        // 审批时效：H1=2d、H2=1d、H3=3d（超SLA）、H4=1d、H5=2d → 均值 ~1.8d
        var quoH1 = new CrmQuote
        {
            QuoteCode = "QUO-HIST-001", InquiryId = inqH1.InquiryId,
            CustomerId = customerSuning.CustomerId, Status = "approved",
            TotalAmount = 9998000m, Currency = "CNY", ValidUntil = m5.AddDays(30),
            Terms = "款到发货", CreatedBy = 2,
            CreatedAt = m5.AddDays(-20), UpdatedAt = m5.AddDays(-18)   // 2d approval
        };
        var quoH2 = new CrmQuote
        {
            QuoteCode = "QUO-HIST-002", InquiryId = inqH2.InquiryId,
            CustomerId = customerHuawei.CustomerId, Status = "approved",
            TotalAmount = 580000m, Currency = "CNY", CreatedBy = 2,
            CreatedAt = m4.AddDays(-14), UpdatedAt = m4.AddDays(-13)   // 1d approval
        };
        var quoH3 = new CrmQuote
        {
            QuoteCode = "QUO-HIST-003", InquiryId = inqH3.InquiryId,
            CustomerId = customerAlibaba.CustomerId, Status = "approved",
            TotalAmount = 1350000m, Currency = "CNY", CreatedBy = 3,
            CreatedAt = m3.AddDays(-18), UpdatedAt = m3.AddDays(-15)   // 3d approval（超SLA）
        };
        var quoH4 = new CrmQuote
        {
            QuoteCode = "QUO-HIST-004", InquiryId = inqH4.InquiryId,
            CustomerId = customerByteDance.CustomerId, Status = "approved",
            TotalAmount = 720000m, Currency = "CNY", CreatedBy = 3,
            CreatedAt = m2.AddDays(-14), UpdatedAt = m2.AddDays(-13)   // 1d approval
        };
        var quoH5 = new CrmQuote
        {
            QuoteCode = "QUO-HIST-005", InquiryId = inqH5.InquiryId,
            CustomerId = customerSuning.CustomerId, Status = "approved",
            TotalAmount = 3999000m, Currency = "CNY", CreatedBy = 2,
            CreatedAt = m1.AddDays(-9), UpdatedAt = m1.AddDays(-7)     // 2d approval
        };
        // 京东报价：审批积压（4天前提交，超2天SLA → overdue）
        var quoPendingJD = new CrmQuote
        {
            QuoteCode = "QUO-PENDING-001", InquiryId = inquiryJD.InquiryId,
            CustomerId = customerJD.CustomerId, Status = "pending_approval",
            TotalAmount = 1997500m, Currency = "CNY",
            ValidUntil = now.AddDays(10), ExpectedDelivery = now.AddDays(45),
            Terms = "款到发货，试销结算", OaApprovalId = 3, CreatedBy = 3,
            CreatedAt = now.AddDays(-4), UpdatedAt = now.AddDays(-4)   // 4d → overdue
        };
        db.Quotes.AddRange(quoH1, quoH2, quoH3, quoH4, quoH5, quoPendingJD);
        db.SaveChanges();

        // ── 历史合同（5 条，覆盖 OTD 趋势 5 个月）────────────────────────────────────
        // H1,H2,H4,H5 准时交付；H3 逾期 5 天 → 整体 OTD = 4/5 = 80%
        var conH1 = new CrmContract
        {
            ContractCode = "CON-HIST-001", QuoteId = quoH1.QuoteId,
            CustomerId = customerSuning.CustomerId,
            Title = "苏宁 iPhone 15 Pro 春季采购合同",
            TotalAmount = 9998000m, Currency = "CNY",
            SignDate = m5.AddDays(-15), DeliveryDate = m5.AddDays(2),
            ActualDeliveryDate = m5,                                    // ON TIME（提前2天）
            Status = "completed", OaApprovalId = null,
            CreatedBy = 2, CreatedAt = m5.AddDays(-15), UpdatedAt = m5
        };
        var conH2 = new CrmContract
        {
            ContractCode = "CON-HIST-002", QuoteId = quoH2.QuoteId,
            CustomerId = customerHuawei.CustomerId,
            Title = "华为手机维修配件年度采购合同",
            TotalAmount = 580000m, Currency = "CNY",
            SignDate = m4.AddDays(-10), DeliveryDate = m4.AddDays(3),
            ActualDeliveryDate = m4,                                    // ON TIME（提前3天）
            Status = "completed",
            CreatedBy = 2, CreatedAt = m4.AddDays(-10), UpdatedAt = m4
        };
        var conH3 = new CrmContract
        {
            ContractCode = "CON-HIST-003", QuoteId = quoH3.QuoteId,
            CustomerId = customerAlibaba.CustomerId,
            Title = "阿里巴巴数据中心服务器配件采购合同",
            TotalAmount = 1350000m, Currency = "CNY",
            SignDate = m3.AddDays(-12), DeliveryDate = m3,
            ActualDeliveryDate = m3.AddDays(5),                         // LATE（逾期5天）
            Status = "completed",
            CreatedBy = 3, CreatedAt = m3.AddDays(-12), UpdatedAt = m3.AddDays(5)
        };
        var conH4 = new CrmContract
        {
            ContractCode = "CON-HIST-004", QuoteId = quoH4.QuoteId,
            CustomerId = customerByteDance.CustomerId,
            Title = "字节跳动服务器硬件采购合同",
            TotalAmount = 720000m, Currency = "CNY",
            SignDate = m2.AddDays(-10), DeliveryDate = m2.AddDays(2),
            ActualDeliveryDate = m2.AddDays(-1),                        // ON TIME（提前1天）
            Status = "completed",
            CreatedBy = 3, CreatedAt = m2.AddDays(-10), UpdatedAt = m2.AddDays(-1)
        };
        var conH5 = new CrmContract
        {
            ContractCode = "CON-HIST-005", QuoteId = quoH5.QuoteId,
            CustomerId = customerSuning.CustomerId,
            Title = "苏宁小米 14 Q1 批次采购合同",
            TotalAmount = 3999000m, Currency = "CNY",
            SignDate = m1.AddDays(-5), DeliveryDate = m1.AddDays(5),
            ActualDeliveryDate = m1,                                    // ON TIME（提前5天）
            Status = "shipped",
            CreatedBy = 2, CreatedAt = m1.AddDays(-5), UpdatedAt = m1
        };
        db.Contracts.AddRange(conH1, conH2, conH3, conH4, conH5);
        db.SaveChanges();

        // ── 历史回款（H1~H4 全额收讫，H5 已收90%，余款审批积压）──────────────────────
        db.Payments.AddRange(
            new CrmPayment
            {
                ContractId = conH1.ContractId, Amount = 9998000m,
                PaymentMethod = "银行转账", PaymentDate = m5.AddDays(10),
                Status = "confirmed", BankReference = "TXN-HIST-001",
                Remark = "全额收款", CreatedBy = 2, CreatedAt = m5.AddDays(10)
            },
            new CrmPayment
            {
                ContractId = conH2.ContractId, Amount = 580000m,
                PaymentMethod = "银行转账", PaymentDate = m4.AddDays(15),
                Status = "confirmed", BankReference = "TXN-HIST-002",
                Remark = "全额收款", CreatedBy = 2, CreatedAt = m4.AddDays(15)
            },
            new CrmPayment
            {
                ContractId = conH3.ContractId, Amount = 1350000m,
                PaymentMethod = "银行转账", PaymentDate = m3.AddDays(20),
                Status = "confirmed", BankReference = "TXN-HIST-003",
                Remark = "逾期交付后全额收款", CreatedBy = 3, CreatedAt = m3.AddDays(20)
            },
            new CrmPayment
            {
                ContractId = conH4.ContractId, Amount = 720000m,
                PaymentMethod = "银行转账", PaymentDate = m2.AddDays(10),
                Status = "confirmed", BankReference = "TXN-HIST-004",
                Remark = "全额收款", CreatedBy = 3, CreatedAt = m2.AddDays(10)
            },
            // H5：90% 已收款（9,000 台 × 3999 × 90%）
            new CrmPayment
            {
                ContractId = conH5.ContractId, Amount = 3599100m,
                PaymentMethod = "银行转账", PaymentDate = m1.AddDays(8),
                Status = "confirmed", BankReference = "TXN-HIST-005",
                Remark = "首付90%款项", CreatedBy = 2, CreatedAt = m1.AddDays(8)
            },
            // H5 余款（10%）：审批积压（4天前提交，超2天SLA → overdue）
            new CrmPayment
            {
                ContractId = conH5.ContractId, Amount = 399900m,
                PaymentMethod = "银行转账", Status = "pending_approval",
                Remark = "小米 14 余款收款申请，待财务审批", CreatedBy = 2,
                CreatedAt = now.AddDays(-4)
            }
        );
        db.SaveChanges();
    }
}
