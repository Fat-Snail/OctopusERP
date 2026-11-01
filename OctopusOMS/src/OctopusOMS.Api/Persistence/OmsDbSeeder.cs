namespace OctopusOMS.Api.Persistence;

public static class OmsDbSeeder
{
    public static void Seed(OmsDbContext db)
    {
        if (db.Channels.Any()) return;
        var now = DateTime.UtcNow;

        db.Channels.AddRange(
            new OmsChannel { ChannelId = 1, ChannelCode = "TAOBAO",  ChannelName = "淘宝/天猫",   ChannelType = "marketplace", Status = "active", CreatedAt = now },
            new OmsChannel { ChannelId = 2, ChannelCode = "JD",      ChannelName = "京东 JD",     ChannelType = "marketplace", Status = "active", CreatedAt = now },
            new OmsChannel { ChannelId = 3, ChannelCode = "PDD",     ChannelName = "拼多多",       ChannelType = "marketplace", Status = "active", CreatedAt = now },
            new OmsChannel { ChannelId = 4, ChannelCode = "DOUYIN",  ChannelName = "抖音小店",     ChannelType = "marketplace", Status = "active", CreatedAt = now },
            new OmsChannel { ChannelId = 5, ChannelCode = "SHOPIFY", ChannelName = "Shopify 独立站", ChannelType = "self_built",  Status = "active", CreatedAt = now },
            new OmsChannel { ChannelId = 6, ChannelCode = "AMAZON",  ChannelName = "Amazon 跨境",  ChannelType = "marketplace", Status = "active", CreatedAt = now }
        );

        db.Shops.AddRange(
            new OmsShop { ShopId = 1, ChannelId = 1, ShopCode = "TB-FLAGSHIP", ShopName = "章鱼旗舰店",     Region = "国内",   Currency = "CNY", Status = "active", CreatedAt = now },
            new OmsShop { ShopId = 2, ChannelId = 1, ShopCode = "TM-OFFICIAL", ShopName = "章鱼天猫官方",   Region = "国内",   Currency = "CNY", Status = "active", CreatedAt = now },
            new OmsShop { ShopId = 3, ChannelId = 2, ShopCode = "JD-FLAGSHIP", ShopName = "章鱼京东自营",   Region = "国内",   Currency = "CNY", Status = "active", CreatedAt = now },
            new OmsShop { ShopId = 4, ChannelId = 4, ShopCode = "DY-OFFICIAL", ShopName = "章鱼抖音官方",   Region = "国内",   Currency = "CNY", Status = "active", CreatedAt = now },
            new OmsShop { ShopId = 5, ChannelId = 6, ShopCode = "AMZ-US",      ShopName = "Octopus Amazon US", Region = "海外", Currency = "USD", Status = "active", CreatedAt = now }
        );

        db.SkuMappings.AddRange(
            new OmsSkuMapping { MappingId = 1, ChannelId = 1, ChannelSkuCode = "TB-IPH15PRO-256", PlmProductId = 1, Note = "iPhone 15 Pro 256GB", CreatedAt = now },
            new OmsSkuMapping { MappingId = 2, ChannelId = 1, ChannelSkuCode = "TB-MI14-256",     PlmProductId = 2, Note = "小米 14 256GB",       CreatedAt = now },
            new OmsSkuMapping { MappingId = 3, ChannelId = 2, ChannelSkuCode = "JD-IPH15PRO",     PlmProductId = 1, Note = "iPhone 15 Pro 京东", CreatedAt = now },
            new OmsSkuMapping { MappingId = 4, ChannelId = 4, ChannelSkuCode = "DY-IPH15PRO",     PlmProductId = 1, Note = "iPhone 15 Pro 抖音", CreatedAt = now }
        );

        db.SyncUsers.AddRange(
            new OmsSyncUser { Id = 1, UmcUserId = 1, UserName = "admin", NickName = "超级管理员", Email = "admin@octopus.com", LastSyncAt = now },
            new OmsSyncUser { Id = 2, UmcUserId = 2, UserName = "zhangsan", NickName = "张三", Email = "zhangsan@octopus.com", LastSyncAt = now }
        );

        // 一条种子订单（淘宝旗舰店，已支付）
        var seedOrder = new OmsOrder
        {
            OrderId = 1,
            OrderNo = $"OMS-{now:yyyyMMdd}-0001",
            ShopId = 1, ChannelId = 1,
            ExternalOrderNo = "TB202611200001",
            CustomerName = "测试买家",
            CustomerPhone = "138****8888",
            ShipAddress = "上海市浦东新区芳甸路 1099 号",
            TotalAmount = 9999m, PaidAmount = 9999m,
            Currency = "CNY",
            Status = "paid",
            OrderDate = now.AddHours(-2),
            PaidAt = now.AddHours(-1),
            CreatedBy = 2,
            CreatedAt = now.AddHours(-2),
            UpdatedAt = now.AddHours(-1),
        };
        seedOrder.Items.Add(new OmsOrderItem
        {
            ChannelSkuCode = "TB-IPH15PRO-256",
            PlmProductId = 1,
            ProductName = "iPhone 15 Pro",
            Spec = "256GB 钛金色",
            Quantity = 1, UnitPrice = 9999m, Amount = 9999m,
        });
        db.Orders.Add(seedOrder);

        db.SaveChanges();
    }
}
