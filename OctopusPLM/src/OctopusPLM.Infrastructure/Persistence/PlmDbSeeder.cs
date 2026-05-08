using OctopusPLM.Core.Entities;

namespace OctopusPLM.Infrastructure.Persistence;

public static class PlmDbSeeder
{
    public static void Seed(PlmDbContext db)
    {
        if (db.SyncUsers.Any()) return; // 幂等

        // ── 种子：默认管理员用户 ──
        db.SyncUsers.Add(new SyncUser
        {
            UmcUserId = 1,
            UserName = "admin",
            NickName = "超级管理员",
            Email = "admin@octopus.com",
            Status = 1,
            PlmRoles = "plm_admin",
            LastSyncAt = DateTime.UtcNow,
        });

        // ── 种子：示例类目 ──
        var catDigital = new Category { Name = "数码产品", Path = "001", Level = 1, OrderNum = 1 };
        var catApparel = new Category { Name = "服装", Path = "002", Level = 1, OrderNum = 2 };
        var catFood = new Category { Name = "食品饮料", Path = "003", Level = 1, OrderNum = 3 };
        var catChemical = new Category { Name = "化工原料", Path = "004", Level = 1, OrderNum = 4 };

        db.Categories.AddRange(catDigital, catApparel, catFood, catChemical);
        db.SaveChanges();

        var catPhone = new Category { Name = "智能手机", ParentId = catDigital.CategoryId, Path = "001.001", Level = 2, OrderNum = 1 };
        var catLaptop = new Category { Name = "笔记本电脑", ParentId = catDigital.CategoryId, Path = "001.002", Level = 2, OrderNum = 2 };
        var catTshirt = new Category { Name = "T恤", ParentId = catApparel.CategoryId, Path = "002.001", Level = 2, OrderNum = 1 };
        var catDress = new Category { Name = "连衣裙", ParentId = catApparel.CategoryId, Path = "002.002", Level = 2, OrderNum = 2 };
        var catSnack = new Category { Name = "休闲零食", ParentId = catFood.CategoryId, Path = "003.001", Level = 2, OrderNum = 1 };
        var catAdditive = new Category { Name = "食品添加剂", ParentId = catChemical.CategoryId, Path = "004.001", Level = 2, OrderNum = 1 };

        db.Categories.AddRange(catPhone, catLaptop, catTshirt, catDress, catSnack, catAdditive);
        db.SaveChanges();

        // ── 种子：类目模型版本（V2 元模型底座） ──
        var mvPhone = new CategoryModelVersion { CategoryId = catPhone.CategoryId, VersionNo = "v1", Status = "active", ChangeSummary = "智能手机首版动态模型", PublishedBy = 1, PublishedAt = DateTime.UtcNow };
        var mvLaptop = new CategoryModelVersion { CategoryId = catLaptop.CategoryId, VersionNo = "v1", Status = "active", ChangeSummary = "笔记本电脑首版动态模型", PublishedBy = 1, PublishedAt = DateTime.UtcNow };
        var mvTshirt = new CategoryModelVersion { CategoryId = catTshirt.CategoryId, VersionNo = "v1", Status = "active", ChangeSummary = "T恤首版动态模型", PublishedBy = 1, PublishedAt = DateTime.UtcNow };
        var mvDress = new CategoryModelVersion { CategoryId = catDress.CategoryId, VersionNo = "v1", Status = "active", ChangeSummary = "连衣裙首版动态模型", PublishedBy = 1, PublishedAt = DateTime.UtcNow };
        var mvSnack = new CategoryModelVersion { CategoryId = catSnack.CategoryId, VersionNo = "v1", Status = "active", ChangeSummary = "休闲零食首版动态模型", PublishedBy = 1, PublishedAt = DateTime.UtcNow };
        var mvAdditive = new CategoryModelVersion { CategoryId = catAdditive.CategoryId, VersionNo = "v1", Status = "active", ChangeSummary = "食品添加剂首版动态模型", PublishedBy = 1, PublishedAt = DateTime.UtcNow };

        db.CategoryModelVersions.AddRange(mvPhone, mvLaptop, mvTshirt, mvDress, mvSnack, mvAdditive);
        db.SaveChanges();

        // ── 种子：属性定义 ──
        var attrColor = new PlmAttribute { Code = "color", Name = "颜色", AttributeType = "enum", InputType = "dropdown", IsRequired = true };
        var attrSize = new PlmAttribute { Code = "size", Name = "尺码", AttributeType = "enum", InputType = "dropdown", IsRequired = true };
        var attrFabric = new PlmAttribute { Code = "fabric", Name = "面料成分", AttributeType = "text", InputType = "single_line" };
        var attrCpu = new PlmAttribute { Code = "cpu_model", Name = "CPU 型号", AttributeType = "text", InputType = "single_line" };
        var attrStorage = new PlmAttribute { Code = "storage_capacity", Name = "存储容量", AttributeType = "enum", InputType = "dropdown", IsRequired = true };
        var attrRam = new PlmAttribute { Code = "ram", Name = "内存", AttributeType = "enum", InputType = "dropdown", IsRequired = true };
        var attrScreen = new PlmAttribute { Code = "screen_size", Name = "屏幕尺寸", AttributeType = "text", InputType = "single_line", Unit = "英寸" };
        var attrWeight = new PlmAttribute { Code = "net_weight", Name = "净重", AttributeType = "text", InputType = "single_line", Unit = "g" };
        var attrShelfLife = new PlmAttribute { Code = "shelf_life", Name = "保质期", AttributeType = "text", InputType = "single_line", Unit = "天" };
        var attrTaste = new PlmAttribute { Code = "taste", Name = "口味", AttributeType = "enum", InputType = "dropdown" };
        var attrCasNo = new PlmAttribute { Code = "cas_no", Name = "CAS 号", AttributeType = "text", InputType = "single_line" };
        var attrPurity = new PlmAttribute { Code = "purity", Name = "纯度", AttributeType = "text", InputType = "single_line", Unit = "%" };

        db.Attributes.AddRange(attrColor, attrSize, attrFabric, attrCpu, attrStorage, attrRam, attrScreen, attrWeight, attrShelfLife, attrTaste, attrCasNo, attrPurity);
        db.SaveChanges();

        // ── 种子：属性枚举值 ──
        db.AttributeValues.AddRange(
            // 颜色
            new AttributeValue { AttributeId = attrColor.AttributeId, Label = "红色", Value = "red", OrderNum = 1 },
            new AttributeValue { AttributeId = attrColor.AttributeId, Label = "蓝色", Value = "blue", OrderNum = 2 },
            new AttributeValue { AttributeId = attrColor.AttributeId, Label = "黑色", Value = "black", OrderNum = 3 },
            new AttributeValue { AttributeId = attrColor.AttributeId, Label = "白色", Value = "white", OrderNum = 4 },
            // 尺码
            new AttributeValue { AttributeId = attrSize.AttributeId, Label = "S", Value = "S", OrderNum = 1 },
            new AttributeValue { AttributeId = attrSize.AttributeId, Label = "M", Value = "M", OrderNum = 2 },
            new AttributeValue { AttributeId = attrSize.AttributeId, Label = "L", Value = "L", OrderNum = 3 },
            new AttributeValue { AttributeId = attrSize.AttributeId, Label = "XL", Value = "XL", OrderNum = 4 },
            // 存储容量
            new AttributeValue { AttributeId = attrStorage.AttributeId, Label = "128GB", Value = "128", OrderNum = 1 },
            new AttributeValue { AttributeId = attrStorage.AttributeId, Label = "256GB", Value = "256", OrderNum = 2 },
            new AttributeValue { AttributeId = attrStorage.AttributeId, Label = "512GB", Value = "512", OrderNum = 3 },
            new AttributeValue { AttributeId = attrStorage.AttributeId, Label = "1TB", Value = "1024", OrderNum = 4 },
            // 内存
            new AttributeValue { AttributeId = attrRam.AttributeId, Label = "8GB", Value = "8", OrderNum = 1 },
            new AttributeValue { AttributeId = attrRam.AttributeId, Label = "16GB", Value = "16", OrderNum = 2 },
            new AttributeValue { AttributeId = attrRam.AttributeId, Label = "32GB", Value = "32", OrderNum = 3 },
            // 口味
            new AttributeValue { AttributeId = attrTaste.AttributeId, Label = "原味", Value = "original", OrderNum = 1 },
            new AttributeValue { AttributeId = attrTaste.AttributeId, Label = "麻辣", Value = "spicy", OrderNum = 2 },
            new AttributeValue { AttributeId = attrTaste.AttributeId, Label = "甜味", Value = "sweet", OrderNum = 3 }
        );
        db.SaveChanges();

        // ── 种子：类目-属性绑定 ──
        // 智能手机 → CPU, 存储, 内存, 屏幕尺寸, 净重（存储+内存为 SKU 维度）
        db.CategoryAttributes.AddRange(
            new CategoryAttribute { CategoryId = catPhone.CategoryId, ModelVersionId = mvPhone.ModelVersionId, AttributeId = attrCpu.AttributeId, OrderNum = 1, GroupType = "basic" },
            new CategoryAttribute { CategoryId = catPhone.CategoryId, ModelVersionId = mvPhone.ModelVersionId, AttributeId = attrStorage.AttributeId, IsRequired = true, OrderNum = 2, GroupName = "配置", GroupType = "sale", IsSaleAxis = true },
            new CategoryAttribute { CategoryId = catPhone.CategoryId, ModelVersionId = mvPhone.ModelVersionId, AttributeId = attrRam.AttributeId, IsRequired = true, OrderNum = 3, GroupName = "配置", GroupType = "sale", IsSaleAxis = true },
            new CategoryAttribute { CategoryId = catPhone.CategoryId, ModelVersionId = mvPhone.ModelVersionId, AttributeId = attrScreen.AttributeId, OrderNum = 4, GroupType = "basic" },
            new CategoryAttribute { CategoryId = catPhone.CategoryId, ModelVersionId = mvPhone.ModelVersionId, AttributeId = attrWeight.AttributeId, OrderNum = 5, GroupType = "logistics" }
        );

        // 笔记本电脑 → CPU, 存储, 内存, 屏幕尺寸, 净重（存储+内存为 SKU 维度）
        db.CategoryAttributes.AddRange(
            new CategoryAttribute { CategoryId = catLaptop.CategoryId, ModelVersionId = mvLaptop.ModelVersionId, AttributeId = attrCpu.AttributeId, OrderNum = 1, GroupType = "basic" },
            new CategoryAttribute { CategoryId = catLaptop.CategoryId, ModelVersionId = mvLaptop.ModelVersionId, AttributeId = attrStorage.AttributeId, IsRequired = true, OrderNum = 2, GroupName = "配置", GroupType = "sale", IsSaleAxis = true },
            new CategoryAttribute { CategoryId = catLaptop.CategoryId, ModelVersionId = mvLaptop.ModelVersionId, AttributeId = attrRam.AttributeId, IsRequired = true, OrderNum = 3, GroupName = "配置", GroupType = "sale", IsSaleAxis = true },
            new CategoryAttribute { CategoryId = catLaptop.CategoryId, ModelVersionId = mvLaptop.ModelVersionId, AttributeId = attrScreen.AttributeId, OrderNum = 4, GroupType = "basic" },
            new CategoryAttribute { CategoryId = catLaptop.CategoryId, ModelVersionId = mvLaptop.ModelVersionId, AttributeId = attrWeight.AttributeId, OrderNum = 5, GroupType = "logistics" }
        );

        // T恤 → 颜色, 尺码, 面料成分（颜色+尺码为 SKU 维度）
        db.CategoryAttributes.AddRange(
            new CategoryAttribute { CategoryId = catTshirt.CategoryId, ModelVersionId = mvTshirt.ModelVersionId, AttributeId = attrColor.AttributeId, IsRequired = true, OrderNum = 1, GroupName = "规格", GroupType = "sale", IsSaleAxis = true },
            new CategoryAttribute { CategoryId = catTshirt.CategoryId, ModelVersionId = mvTshirt.ModelVersionId, AttributeId = attrSize.AttributeId, IsRequired = true, OrderNum = 2, GroupName = "规格", GroupType = "sale", IsSaleAxis = true },
            new CategoryAttribute { CategoryId = catTshirt.CategoryId, ModelVersionId = mvTshirt.ModelVersionId, AttributeId = attrFabric.AttributeId, OrderNum = 3, GroupType = "basic" }
        );

        // 连衣裙 → 颜色, 尺码, 面料成分
        db.CategoryAttributes.AddRange(
            new CategoryAttribute { CategoryId = catDress.CategoryId, ModelVersionId = mvDress.ModelVersionId, AttributeId = attrColor.AttributeId, IsRequired = true, OrderNum = 1, GroupName = "规格", GroupType = "sale", IsSaleAxis = true },
            new CategoryAttribute { CategoryId = catDress.CategoryId, ModelVersionId = mvDress.ModelVersionId, AttributeId = attrSize.AttributeId, IsRequired = true, OrderNum = 2, GroupName = "规格", GroupType = "sale", IsSaleAxis = true },
            new CategoryAttribute { CategoryId = catDress.CategoryId, ModelVersionId = mvDress.ModelVersionId, AttributeId = attrFabric.AttributeId, OrderNum = 3, GroupType = "basic" }
        );

        // 休闲零食 → 净重, 保质期, 口味
        db.CategoryAttributes.AddRange(
            new CategoryAttribute { CategoryId = catSnack.CategoryId, ModelVersionId = mvSnack.ModelVersionId, AttributeId = attrWeight.AttributeId, IsRequired = true, OrderNum = 1, GroupType = "logistics" },
            new CategoryAttribute { CategoryId = catSnack.CategoryId, ModelVersionId = mvSnack.ModelVersionId, AttributeId = attrShelfLife.AttributeId, IsRequired = true, OrderNum = 2, GroupType = "compliance" },
            new CategoryAttribute { CategoryId = catSnack.CategoryId, ModelVersionId = mvSnack.ModelVersionId, AttributeId = attrTaste.AttributeId, OrderNum = 3, GroupName = "规格", GroupType = "sale", IsSaleAxis = true }
        );

        // 食品添加剂 → CAS号, 纯度, 净重, 保质期
        db.CategoryAttributes.AddRange(
            new CategoryAttribute { CategoryId = catAdditive.CategoryId, ModelVersionId = mvAdditive.ModelVersionId, AttributeId = attrCasNo.AttributeId, IsRequired = true, OrderNum = 1, GroupType = "compliance" },
            new CategoryAttribute { CategoryId = catAdditive.CategoryId, ModelVersionId = mvAdditive.ModelVersionId, AttributeId = attrPurity.AttributeId, IsRequired = true, OrderNum = 2, GroupType = "compliance" },
            new CategoryAttribute { CategoryId = catAdditive.CategoryId, ModelVersionId = mvAdditive.ModelVersionId, AttributeId = attrWeight.AttributeId, OrderNum = 3, GroupType = "logistics" },
            new CategoryAttribute { CategoryId = catAdditive.CategoryId, ModelVersionId = mvAdditive.ModelVersionId, AttributeId = attrShelfLife.AttributeId, OrderNum = 4, GroupType = "compliance" }
        );

        db.SaveChanges();

        // ── 种子：详情组件、规则、1688 映射样例 ──
        var cGallery = new DetailComponentDef { ComponentCode = "gallery", ComponentName = "主图轮播", ComponentType = "gallery" };
        var cSellingPoints = new DetailComponentDef { ComponentCode = "selling_points", ComponentName = "图文卖点", ComponentType = "rich_text" };
        var cParamTable = new DetailComponentDef { ComponentCode = "param_table", ComponentName = "参数表", ComponentType = "param_table" };
        var cSpecTable = new DetailComponentDef { ComponentCode = "spec_table", ComponentName = "尺码/规格表", ComponentType = "param_table" };
        var cCertificate = new DetailComponentDef { ComponentCode = "certificate", ComponentName = "资质证照", ComponentType = "certificate" };
        var cFaq = new DetailComponentDef { ComponentCode = "faq", ComponentName = "FAQ", ComponentType = "faq" };

        db.DetailComponentDefs.AddRange(cGallery, cSellingPoints, cParamTable, cSpecTable, cCertificate, cFaq);
        db.SaveChanges();

        var tPhone = new DetailTemplate { ModelVersionId = mvPhone.ModelVersionId, TemplateName = "智能手机默认详情模板", Status = "active" };
        var tLaptop = new DetailTemplate { ModelVersionId = mvLaptop.ModelVersionId, TemplateName = "笔记本电脑默认详情模板", Status = "active" };
        var tTshirt = new DetailTemplate { ModelVersionId = mvTshirt.ModelVersionId, TemplateName = "T恤默认详情模板", Status = "active" };
        var tDress = new DetailTemplate { ModelVersionId = mvDress.ModelVersionId, TemplateName = "连衣裙默认详情模板", Status = "active" };
        var tSnack = new DetailTemplate { ModelVersionId = mvSnack.ModelVersionId, TemplateName = "休闲零食默认详情模板", Status = "active" };
        var tAdditive = new DetailTemplate { ModelVersionId = mvAdditive.ModelVersionId, TemplateName = "食品添加剂默认详情模板", Status = "active" };

        db.DetailTemplates.AddRange(tPhone, tLaptop, tTshirt, tDress, tSnack, tAdditive);
        db.SaveChanges();

        db.DetailComponentBinds.AddRange(
            new DetailComponentBind { TemplateId = tPhone.TemplateId, ComponentId = cGallery.ComponentId, OrderNum = 1, IsRequired = true },
            new DetailComponentBind { TemplateId = tPhone.TemplateId, ComponentId = cSellingPoints.ComponentId, OrderNum = 2, IsRequired = true },
            new DetailComponentBind { TemplateId = tPhone.TemplateId, ComponentId = cParamTable.ComponentId, OrderNum = 3, IsRequired = true },
            new DetailComponentBind { TemplateId = tPhone.TemplateId, ComponentId = cFaq.ComponentId, OrderNum = 4 },
            new DetailComponentBind { TemplateId = tLaptop.TemplateId, ComponentId = cGallery.ComponentId, OrderNum = 1, IsRequired = true },
            new DetailComponentBind { TemplateId = tLaptop.TemplateId, ComponentId = cParamTable.ComponentId, OrderNum = 2, IsRequired = true },
            new DetailComponentBind { TemplateId = tLaptop.TemplateId, ComponentId = cFaq.ComponentId, OrderNum = 3 },
            new DetailComponentBind { TemplateId = tTshirt.TemplateId, ComponentId = cGallery.ComponentId, OrderNum = 1, IsRequired = true },
            new DetailComponentBind { TemplateId = tTshirt.TemplateId, ComponentId = cSpecTable.ComponentId, OrderNum = 2, IsRequired = true },
            new DetailComponentBind { TemplateId = tTshirt.TemplateId, ComponentId = cSellingPoints.ComponentId, OrderNum = 3 },
            new DetailComponentBind { TemplateId = tDress.TemplateId, ComponentId = cGallery.ComponentId, OrderNum = 1, IsRequired = true },
            new DetailComponentBind { TemplateId = tDress.TemplateId, ComponentId = cSpecTable.ComponentId, OrderNum = 2, IsRequired = true },
            new DetailComponentBind { TemplateId = tDress.TemplateId, ComponentId = cSellingPoints.ComponentId, OrderNum = 3 },
            new DetailComponentBind { TemplateId = tSnack.TemplateId, ComponentId = cGallery.ComponentId, OrderNum = 1, IsRequired = true },
            new DetailComponentBind { TemplateId = tSnack.TemplateId, ComponentId = cParamTable.ComponentId, OrderNum = 2, IsRequired = true },
            new DetailComponentBind { TemplateId = tSnack.TemplateId, ComponentId = cCertificate.ComponentId, OrderNum = 3, IsRequired = true },
            new DetailComponentBind { TemplateId = tSnack.TemplateId, ComponentId = cFaq.ComponentId, OrderNum = 4 },
            new DetailComponentBind { TemplateId = tAdditive.TemplateId, ComponentId = cParamTable.ComponentId, OrderNum = 1, IsRequired = true },
            new DetailComponentBind { TemplateId = tAdditive.TemplateId, ComponentId = cCertificate.ComponentId, OrderNum = 2, IsRequired = true }
        );

        db.RuleDefs.AddRange(
            new RuleDef { ModelVersionId = mvPhone.ModelVersionId, RuleCode = "phone_sku_axes_required", RuleName = "手机 SKU 轴必填", RuleType = "publish_check", ActionExpr = "require(storage_capacity,ram)", Message = "手机类目必须填写存储容量和内存" },
            new RuleDef { ModelVersionId = mvSnack.ModelVersionId, RuleCode = "food_certificate_required", RuleName = "食品资质必填", RuleType = "publish_check", ActionExpr = "require_detail_component(certificate)", Message = "食品类商品必须上传资质证照" }
        );

        var channel1688 = new ChannelDef { ChannelCode = "1688", ChannelName = "1688" };
        db.ChannelDefs.Add(channel1688);
        db.SaveChanges();

        var mapPhone1688 = new ChannelCategoryMapping { ChannelId = channel1688.ChannelId, CategoryId = catPhone.CategoryId, ExternalCategoryId = "1688_phone", ExternalCategoryName = "手机" };
        var mapTshirt1688 = new ChannelCategoryMapping { ChannelId = channel1688.ChannelId, CategoryId = catTshirt.CategoryId, ExternalCategoryId = "1688_tshirt", ExternalCategoryName = "T恤" };
        var mapSnack1688 = new ChannelCategoryMapping { ChannelId = channel1688.ChannelId, CategoryId = catSnack.CategoryId, ExternalCategoryId = "1688_snack", ExternalCategoryName = "休闲零食" };
        db.ChannelCategoryMappings.AddRange(mapPhone1688, mapTshirt1688, mapSnack1688);
        db.SaveChanges();

        db.ChannelAttributeMappings.AddRange(
            new ChannelAttributeMapping { ChannelCategoryMappingId = mapPhone1688.MappingId, AttributeId = attrStorage.AttributeId, ExternalAttributeId = "storage", ExternalAttributeName = "存储容量", IsRequiredOutbound = true },
            new ChannelAttributeMapping { ChannelCategoryMappingId = mapPhone1688.MappingId, AttributeId = attrRam.AttributeId, ExternalAttributeId = "memory", ExternalAttributeName = "运行内存", IsRequiredOutbound = true },
            new ChannelAttributeMapping { ChannelCategoryMappingId = mapPhone1688.MappingId, AttributeId = attrScreen.AttributeId, ExternalAttributeId = "screen_size", ExternalAttributeName = "屏幕尺寸", IsRequiredOutbound = false },
            new ChannelAttributeMapping { ChannelCategoryMappingId = mapTshirt1688.MappingId, AttributeId = attrColor.AttributeId, ExternalAttributeId = "color", ExternalAttributeName = "颜色", IsRequiredOutbound = true },
            new ChannelAttributeMapping { ChannelCategoryMappingId = mapTshirt1688.MappingId, AttributeId = attrSize.AttributeId, ExternalAttributeId = "size", ExternalAttributeName = "尺码", IsRequiredOutbound = true },
            new ChannelAttributeMapping { ChannelCategoryMappingId = mapTshirt1688.MappingId, AttributeId = attrFabric.AttributeId, ExternalAttributeId = "fabric_detail", ExternalAttributeName = "面料成分", IsRequiredOutbound = false },
            new ChannelAttributeMapping { ChannelCategoryMappingId = mapSnack1688.MappingId, AttributeId = attrShelfLife.AttributeId, ExternalAttributeId = "shelf_life", ExternalAttributeName = "保质期", IsRequiredOutbound = true },
            new ChannelAttributeMapping { ChannelCategoryMappingId = mapSnack1688.MappingId, AttributeId = attrTaste.AttributeId, ExternalAttributeId = "taste_flavor", ExternalAttributeName = "口味口感", IsRequiredOutbound = false },
            new ChannelAttributeMapping { ChannelCategoryMappingId = mapSnack1688.MappingId, AttributeId = attrWeight.AttributeId, ExternalAttributeId = "net_weight_g", ExternalAttributeName = "净含量(g)", IsRequiredOutbound = true }
        );
        db.SaveChanges();

        // ── 渠道种子：淘宝（B2C 消费级，属性命名以中文描述为主）──
        var channelTaobao = new ChannelDef { ChannelCode = "taobao", ChannelName = "淘宝" };
        db.ChannelDefs.Add(channelTaobao);
        db.SaveChanges();

        var mapPhoneTB = new ChannelCategoryMapping { ChannelId = channelTaobao.ChannelId, CategoryId = catPhone.CategoryId, ExternalCategoryId = "TB_PHONE_01", ExternalCategoryName = "手机/智能手机" };
        var mapLaptopTB = new ChannelCategoryMapping { ChannelId = channelTaobao.ChannelId, CategoryId = catLaptop.CategoryId, ExternalCategoryId = "TB_LAPTOP_02", ExternalCategoryName = "电脑/笔记本" };
        var mapTshirtTB = new ChannelCategoryMapping { ChannelId = channelTaobao.ChannelId, CategoryId = catTshirt.CategoryId, ExternalCategoryId = "TB_TOPS_TSHIRT", ExternalCategoryName = "上衣/T恤" };
        var mapDressTB  = new ChannelCategoryMapping { ChannelId = channelTaobao.ChannelId, CategoryId = catDress.CategoryId,  ExternalCategoryId = "TB_DRESS_001",   ExternalCategoryName = "连衣裙/裙装" };
        var mapSnackTB  = new ChannelCategoryMapping { ChannelId = channelTaobao.ChannelId, CategoryId = catSnack.CategoryId,  ExternalCategoryId = "TB_FOOD_SNACK",  ExternalCategoryName = "休闲食品/零食" };
        db.ChannelCategoryMappings.AddRange(mapPhoneTB, mapLaptopTB, mapTshirtTB, mapDressTB, mapSnackTB);
        db.SaveChanges();

        db.ChannelAttributeMappings.AddRange(
            // 手机 → 淘宝用数字枚举 ID
            new ChannelAttributeMapping { ChannelCategoryMappingId = mapPhoneTB.MappingId, AttributeId = attrStorage.AttributeId, ExternalAttributeId = "prop_20509", ExternalAttributeName = "机身存储", IsRequiredOutbound = true },
            new ChannelAttributeMapping { ChannelCategoryMappingId = mapPhoneTB.MappingId, AttributeId = attrRam.AttributeId,     ExternalAttributeId = "prop_20510", ExternalAttributeName = "运行内存", IsRequiredOutbound = true },
            new ChannelAttributeMapping { ChannelCategoryMappingId = mapPhoneTB.MappingId, AttributeId = attrColor.AttributeId,   ExternalAttributeId = "prop_1627207", ExternalAttributeName = "颜色分类", IsRequiredOutbound = true },
            new ChannelAttributeMapping { ChannelCategoryMappingId = mapPhoneTB.MappingId, AttributeId = attrScreen.AttributeId,  ExternalAttributeId = "prop_20513", ExternalAttributeName = "屏幕尺寸", IsRequiredOutbound = false },
            // 笔记本
            new ChannelAttributeMapping { ChannelCategoryMappingId = mapLaptopTB.MappingId, AttributeId = attrCpu.AttributeId,     ExternalAttributeId = "prop_cpu_model", ExternalAttributeName = "处理器型号", IsRequiredOutbound = true },
            new ChannelAttributeMapping { ChannelCategoryMappingId = mapLaptopTB.MappingId, AttributeId = attrRam.AttributeId,     ExternalAttributeId = "prop_memory",    ExternalAttributeName = "内存容量", IsRequiredOutbound = true },
            new ChannelAttributeMapping { ChannelCategoryMappingId = mapLaptopTB.MappingId, AttributeId = attrStorage.AttributeId, ExternalAttributeId = "prop_storage",   ExternalAttributeName = "硬盘容量", IsRequiredOutbound = true },
            new ChannelAttributeMapping { ChannelCategoryMappingId = mapLaptopTB.MappingId, AttributeId = attrScreen.AttributeId,  ExternalAttributeId = "prop_screen",    ExternalAttributeName = "屏幕尺寸", IsRequiredOutbound = false },
            // T恤
            new ChannelAttributeMapping { ChannelCategoryMappingId = mapTshirtTB.MappingId, AttributeId = attrColor.AttributeId,  ExternalAttributeId = "prop_1627207", ExternalAttributeName = "颜色分类", IsRequiredOutbound = true },
            new ChannelAttributeMapping { ChannelCategoryMappingId = mapTshirtTB.MappingId, AttributeId = attrSize.AttributeId,   ExternalAttributeId = "prop_20755",   ExternalAttributeName = "尺码", IsRequiredOutbound = true },
            new ChannelAttributeMapping { ChannelCategoryMappingId = mapTshirtTB.MappingId, AttributeId = attrFabric.AttributeId, ExternalAttributeId = "prop_fabric",  ExternalAttributeName = "面料成分", IsRequiredOutbound = false },
            // 连衣裙
            new ChannelAttributeMapping { ChannelCategoryMappingId = mapDressTB.MappingId, AttributeId = attrColor.AttributeId, ExternalAttributeId = "prop_1627207", ExternalAttributeName = "颜色分类", IsRequiredOutbound = true },
            new ChannelAttributeMapping { ChannelCategoryMappingId = mapDressTB.MappingId, AttributeId = attrSize.AttributeId,  ExternalAttributeId = "prop_20755",   ExternalAttributeName = "尺码", IsRequiredOutbound = true },
            // 零食
            new ChannelAttributeMapping { ChannelCategoryMappingId = mapSnackTB.MappingId, AttributeId = attrShelfLife.AttributeId, ExternalAttributeId = "prop_shelf_life", ExternalAttributeName = "保质期", IsRequiredOutbound = true },
            new ChannelAttributeMapping { ChannelCategoryMappingId = mapSnackTB.MappingId, AttributeId = attrTaste.AttributeId,     ExternalAttributeId = "prop_taste",      ExternalAttributeName = "口味", IsRequiredOutbound = false },
            new ChannelAttributeMapping { ChannelCategoryMappingId = mapSnackTB.MappingId, AttributeId = attrWeight.AttributeId,    ExternalAttributeId = "prop_net_weight", ExternalAttributeName = "净含量", IsRequiredOutbound = true }
        );
        db.SaveChanges();

        // ── 渠道种子：抖音小店（直播电商，属性 ID 以 dy_ 前缀）──
        var channelDouyin = new ChannelDef { ChannelCode = "douyin", ChannelName = "抖音小店" };
        db.ChannelDefs.Add(channelDouyin);
        db.SaveChanges();

        var mapPhoneDY  = new ChannelCategoryMapping { ChannelId = channelDouyin.ChannelId, CategoryId = catPhone.CategoryId,  ExternalCategoryId = "DY_3C_PHONE",     ExternalCategoryName = "数码/手机" };
        var mapTshirtDY = new ChannelCategoryMapping { ChannelId = channelDouyin.ChannelId, CategoryId = catTshirt.CategoryId, ExternalCategoryId = "DY_CLO_TSHIRT",   ExternalCategoryName = "服饰/T恤" };
        var mapDressDY  = new ChannelCategoryMapping { ChannelId = channelDouyin.ChannelId, CategoryId = catDress.CategoryId,  ExternalCategoryId = "DY_CLO_DRESS",    ExternalCategoryName = "服饰/连衣裙" };
        var mapSnackDY  = new ChannelCategoryMapping { ChannelId = channelDouyin.ChannelId, CategoryId = catSnack.CategoryId,  ExternalCategoryId = "DY_FOOD_LEISURE", ExternalCategoryName = "食品/休闲零食" };
        db.ChannelCategoryMappings.AddRange(mapPhoneDY, mapTshirtDY, mapDressDY, mapSnackDY);
        db.SaveChanges();

        db.ChannelAttributeMappings.AddRange(
            // 手机 → 抖音用 snake_case 字段名
            new ChannelAttributeMapping { ChannelCategoryMappingId = mapPhoneDY.MappingId, AttributeId = attrStorage.AttributeId, ExternalAttributeId = "dy_storage_gb",  ExternalAttributeName = "存储(GB)", IsRequiredOutbound = true },
            new ChannelAttributeMapping { ChannelCategoryMappingId = mapPhoneDY.MappingId, AttributeId = attrRam.AttributeId,     ExternalAttributeId = "dy_ram_gb",      ExternalAttributeName = "内存(GB)", IsRequiredOutbound = true },
            new ChannelAttributeMapping { ChannelCategoryMappingId = mapPhoneDY.MappingId, AttributeId = attrColor.AttributeId,   ExternalAttributeId = "dy_color_name",  ExternalAttributeName = "颜色", IsRequiredOutbound = true },
            // T恤
            new ChannelAttributeMapping { ChannelCategoryMappingId = mapTshirtDY.MappingId, AttributeId = attrColor.AttributeId,  ExternalAttributeId = "dy_color_name", ExternalAttributeName = "颜色", IsRequiredOutbound = true },
            new ChannelAttributeMapping { ChannelCategoryMappingId = mapTshirtDY.MappingId, AttributeId = attrSize.AttributeId,   ExternalAttributeId = "dy_size_std",   ExternalAttributeName = "尺码规格", IsRequiredOutbound = true },
            new ChannelAttributeMapping { ChannelCategoryMappingId = mapTshirtDY.MappingId, AttributeId = attrFabric.AttributeId, ExternalAttributeId = "dy_material",   ExternalAttributeName = "材质工艺", IsRequiredOutbound = false },
            // 连衣裙
            new ChannelAttributeMapping { ChannelCategoryMappingId = mapDressDY.MappingId, AttributeId = attrColor.AttributeId, ExternalAttributeId = "dy_color_name", ExternalAttributeName = "颜色", IsRequiredOutbound = true },
            new ChannelAttributeMapping { ChannelCategoryMappingId = mapDressDY.MappingId, AttributeId = attrSize.AttributeId,  ExternalAttributeId = "dy_size_std",   ExternalAttributeName = "尺码规格", IsRequiredOutbound = true },
            // 零食
            new ChannelAttributeMapping { ChannelCategoryMappingId = mapSnackDY.MappingId, AttributeId = attrShelfLife.AttributeId, ExternalAttributeId = "dy_shelf_days",  ExternalAttributeName = "保质期(天)", IsRequiredOutbound = true },
            new ChannelAttributeMapping { ChannelCategoryMappingId = mapSnackDY.MappingId, AttributeId = attrTaste.AttributeId,     ExternalAttributeId = "dy_flavor_tag",  ExternalAttributeName = "口味标签", IsRequiredOutbound = false },
            new ChannelAttributeMapping { ChannelCategoryMappingId = mapSnackDY.MappingId, AttributeId = attrWeight.AttributeId,    ExternalAttributeId = "dy_weight_gram", ExternalAttributeName = "重量(g)", IsRequiredOutbound = true }
        );

        db.SaveChanges();

        // ── 种子：测试商品（覆盖全部叶子类目 + 全部状态） ──
        var now = DateTime.UtcNow;
        long adminId = 1;

        // 1. 智能手机 — iPhone 15 Pro（active，2 SKU）
        var pIPhone = new Product
        {
            CategoryId = catPhone.CategoryId,
            ModelVersionId = mvPhone.ModelVersionId,
            ProductName = "iPhone 15 Pro",
            Description = "Apple A17 Pro 芯片，钛金属机身，三摄像头系统",
            MainImage = "https://picsum.photos/seed/iphone/600/600",
            Status = "active",
            CreatedBy = adminId,
            CreatedAt = now.AddDays(-30),
            UpdatedAt = now.AddDays(-1),
        };
        pIPhone.SetAttributes(new() { ["CPU 型号"] = "A17 Pro", ["屏幕尺寸"] = "6.1", ["净重"] = "187" });

        // 2. 智能手机 — 小米 14（approved）
        var pMi = new Product
        {
            CategoryId = catPhone.CategoryId,
            ModelVersionId = mvPhone.ModelVersionId,
            ProductName = "小米 14",
            Description = "骁龙 8 Gen 3，徕卡光学镜头",
            MainImage = "https://picsum.photos/seed/mi14/600/600",
            Status = "approved",
            CreatedBy = adminId,
            CreatedAt = now.AddDays(-15),
            UpdatedAt = now.AddDays(-2),
        };
        pMi.SetAttributes(new() { ["CPU 型号"] = "Snapdragon 8 Gen 3", ["屏幕尺寸"] = "6.36", ["净重"] = "193" });

        // 3. 笔记本电脑 — MacBook Pro 14（active）
        var pMac = new Product
        {
            CategoryId = catLaptop.CategoryId,
            ModelVersionId = mvLaptop.ModelVersionId,
            ProductName = "MacBook Pro 14",
            Description = "M3 Pro 芯片，14.2 英寸 Liquid Retina XDR",
            MainImage = "https://picsum.photos/seed/macbook/600/600",
            Status = "active",
            CreatedBy = adminId,
            CreatedAt = now.AddDays(-20),
            UpdatedAt = now.AddDays(-3),
        };
        pMac.SetAttributes(new() { ["CPU 型号"] = "M3 Pro", ["屏幕尺寸"] = "14.2", ["净重"] = "1610" });

        // 4. T 恤 — 纯棉圆领 T 恤（active，2 SKU）
        var pTshirt = new Product
        {
            CategoryId = catTshirt.CategoryId,
            ModelVersionId = mvTshirt.ModelVersionId,
            ProductName = "纯棉圆领 T 恤",
            Description = "100% 新疆长绒棉，男女同款",
            MainImage = "https://picsum.photos/seed/tshirt/600/600",
            Status = "active",
            CreatedBy = adminId,
            CreatedAt = now.AddDays(-10),
            UpdatedAt = now.AddDays(-1),
        };
        pTshirt.SetAttributes(new() { ["面料成分"] = "100% 棉" });

        // 5. 连衣裙 — 法式碎花连衣裙（pending_review）
        var pDress = new Product
        {
            CategoryId = catDress.CategoryId,
            ModelVersionId = mvDress.ModelVersionId,
            ProductName = "法式碎花连衣裙",
            Description = "雪纺面料，方领泡泡袖",
            MainImage = "https://picsum.photos/seed/dress/600/600",
            Status = "pending_review",
            CreatedBy = adminId,
            CreatedAt = now.AddDays(-5),
            UpdatedAt = now.AddDays(-1),
        };
        pDress.SetAttributes(new() { ["面料成分"] = "雪纺 70%、聚酯纤维 30%" });

        // 6. 休闲零食 — 川味麻辣条（active）
        var pSnack = new Product
        {
            CategoryId = catSnack.CategoryId,
            ModelVersionId = mvSnack.ModelVersionId,
            ProductName = "川味麻辣条 100g",
            Description = "经典童年味道，香辣爽口",
            MainImage = "https://picsum.photos/seed/snack/600/600",
            Status = "active",
            CreatedBy = adminId,
            CreatedAt = now.AddDays(-7),
            UpdatedAt = now.AddDays(-1),
        };
        pSnack.SetAttributes(new() { ["净重"] = "100", ["保质期"] = "270", ["口味"] = "spicy" });

        // 7. 食品添加剂 — 柠檬酸（draft）
        var pAdditive = new Product
        {
            CategoryId = catAdditive.CategoryId,
            ModelVersionId = mvAdditive.ModelVersionId,
            ProductName = "食品级柠檬酸",
            Description = "酸味剂，符合 GB1886.235 标准",
            MainImage = "https://picsum.photos/seed/citric/600/600",
            Status = "draft",
            CreatedBy = adminId,
            CreatedAt = now.AddDays(-2),
            UpdatedAt = now.AddDays(-1),
        };
        pAdditive.SetAttributes(new() { ["CAS 号"] = "77-92-9", ["纯度"] = "99.5", ["净重"] = "25000", ["保质期"] = "730" });

        db.Products.AddRange(pIPhone, pMi, pMac, pTshirt, pDress, pSnack, pAdditive);
        db.SaveChanges();

        db.ProductAttributeValues.AddRange(
            new ProductAttributeValue { ProductId = pIPhone.ProductId, AttributeId = attrCpu.AttributeId, ValueText = "A17 Pro" },
            new ProductAttributeValue { ProductId = pIPhone.ProductId, AttributeId = attrScreen.AttributeId, ValueText = "6.1" },
            new ProductAttributeValue { ProductId = pIPhone.ProductId, AttributeId = attrWeight.AttributeId, ValueText = "187", ValueNumber = 187 },
            new ProductAttributeValue { ProductId = pMi.ProductId, AttributeId = attrCpu.AttributeId, ValueText = "Snapdragon 8 Gen 3" },
            new ProductAttributeValue { ProductId = pMi.ProductId, AttributeId = attrScreen.AttributeId, ValueText = "6.36" },
            new ProductAttributeValue { ProductId = pMi.ProductId, AttributeId = attrWeight.AttributeId, ValueText = "193", ValueNumber = 193 },
            new ProductAttributeValue { ProductId = pMac.ProductId, AttributeId = attrCpu.AttributeId, ValueText = "M3 Pro" },
            new ProductAttributeValue { ProductId = pMac.ProductId, AttributeId = attrScreen.AttributeId, ValueText = "14.2" },
            new ProductAttributeValue { ProductId = pMac.ProductId, AttributeId = attrWeight.AttributeId, ValueText = "1610", ValueNumber = 1610 },
            new ProductAttributeValue { ProductId = pTshirt.ProductId, AttributeId = attrFabric.AttributeId, ValueText = "100% 棉" },
            new ProductAttributeValue { ProductId = pDress.ProductId, AttributeId = attrFabric.AttributeId, ValueText = "雪纺 70%、聚酯纤维 30%" },
            new ProductAttributeValue { ProductId = pSnack.ProductId, AttributeId = attrWeight.AttributeId, ValueText = "100", ValueNumber = 100 },
            new ProductAttributeValue { ProductId = pSnack.ProductId, AttributeId = attrShelfLife.AttributeId, ValueText = "270", ValueNumber = 270 },
            new ProductAttributeValue { ProductId = pSnack.ProductId, AttributeId = attrTaste.AttributeId, ValueText = "spicy" },
            new ProductAttributeValue { ProductId = pAdditive.ProductId, AttributeId = attrCasNo.AttributeId, ValueText = "77-92-9" },
            new ProductAttributeValue { ProductId = pAdditive.ProductId, AttributeId = attrPurity.AttributeId, ValueText = "99.5", ValueNumber = 99.5m },
            new ProductAttributeValue { ProductId = pAdditive.ProductId, AttributeId = attrWeight.AttributeId, ValueText = "25000", ValueNumber = 25000 },
            new ProductAttributeValue { ProductId = pAdditive.ProductId, AttributeId = attrShelfLife.AttributeId, ValueText = "730", ValueNumber = 730 }
        );

        // ── 种子：商品图片（每个商品 1 张主图 + 1 张副图） ──
        db.ProductImages.AddRange(
            new ProductImage { ProductId = pIPhone.ProductId, Url = pIPhone.MainImage!, IsMain = true, OrderNum = 1 },
            new ProductImage { ProductId = pIPhone.ProductId, Url = "https://picsum.photos/seed/iphone2/600/600", IsMain = false, OrderNum = 2 },
            new ProductImage { ProductId = pMi.ProductId, Url = pMi.MainImage!, IsMain = true, OrderNum = 1 },
            new ProductImage { ProductId = pMac.ProductId, Url = pMac.MainImage!, IsMain = true, OrderNum = 1 },
            new ProductImage { ProductId = pTshirt.ProductId, Url = pTshirt.MainImage!, IsMain = true, OrderNum = 1 },
            new ProductImage { ProductId = pDress.ProductId, Url = pDress.MainImage!, IsMain = true, OrderNum = 1 },
            new ProductImage { ProductId = pSnack.ProductId, Url = pSnack.MainImage!, IsMain = true, OrderNum = 1 },
            new ProductImage { ProductId = pAdditive.ProductId, Url = pAdditive.MainImage!, IsMain = true, OrderNum = 1 }
        );

        // ── 种子：SKU（销售属性） ──
        var skuIPhone1 = new ProductSku { ProductId = pIPhone.ProductId, SkuCode = "IP15P-256-8", Price = 7999m, CostPrice = 5500m, Stock = 50, Status = 1 };
        skuIPhone1.SetSaleAttributes(new() { ["存储容量"] = "256", ["内存"] = "8" });
        var skuIPhone2 = new ProductSku { ProductId = pIPhone.ProductId, SkuCode = "IP15P-512-8", Price = 9299m, CostPrice = 6300m, Stock = 30, Status = 1 };
        skuIPhone2.SetSaleAttributes(new() { ["存储容量"] = "512", ["内存"] = "8" });

        var skuMi = new ProductSku { ProductId = pMi.ProductId, SkuCode = "MI14-256-12", Price = 4299m, CostPrice = 2900m, Stock = 80, Status = 1 };
        skuMi.SetSaleAttributes(new() { ["存储容量"] = "256", ["内存"] = "16" });

        var skuMac = new ProductSku { ProductId = pMac.ProductId, SkuCode = "MBP14-512-16", Price = 16999m, CostPrice = 12000m, Stock = 15, Status = 1 };
        skuMac.SetSaleAttributes(new() { ["存储容量"] = "512", ["内存"] = "16" });

        var skuTshirt1 = new ProductSku { ProductId = pTshirt.ProductId, SkuCode = "TS-RED-M", Price = 89m, CostPrice = 35m, Stock = 200, Status = 1 };
        skuTshirt1.SetSaleAttributes(new() { ["颜色"] = "red", ["尺码"] = "M" });
        var skuTshirt2 = new ProductSku { ProductId = pTshirt.ProductId, SkuCode = "TS-BLU-L", Price = 89m, CostPrice = 35m, Stock = 150, Status = 1 };
        skuTshirt2.SetSaleAttributes(new() { ["颜色"] = "blue", ["尺码"] = "L" });

        var skuDress = new ProductSku { ProductId = pDress.ProductId, SkuCode = "DR-WHT-S", Price = 299m, CostPrice = 110m, Stock = 60, Status = 1 };
        skuDress.SetSaleAttributes(new() { ["颜色"] = "white", ["尺码"] = "S" });

        var skuSnack = new ProductSku { ProductId = pSnack.ProductId, SkuCode = "SNK-SPCY-100", Price = 5.5m, CostPrice = 2.2m, Stock = 1000, Status = 1 };

        var skuAdditive = new ProductSku { ProductId = pAdditive.ProductId, SkuCode = "CIT-25KG", Price = 320m, CostPrice = 220m, Stock = 40, Status = 1 };

        db.ProductSkus.AddRange(skuIPhone1, skuIPhone2, skuMi, skuMac, skuTshirt1, skuTshirt2, skuDress, skuSnack, skuAdditive);

        // ── 种子：审核记录（pending_review/approved/active 的 submit 记录） ──
        db.ProductReviews.AddRange(
            new ProductReview { ProductId = pIPhone.ProductId, ReviewerId = adminId, ReviewerName = "超级管理员", Action = "submit", Comment = "提交审核", CreatedAt = now.AddDays(-29) },
            new ProductReview { ProductId = pIPhone.ProductId, ReviewerId = adminId, ReviewerName = "超级管理员", Action = "approve", Comment = "通过", CreatedAt = now.AddDays(-28) },
            new ProductReview { ProductId = pMi.ProductId, ReviewerId = adminId, ReviewerName = "超级管理员", Action = "submit", Comment = "提交审核", CreatedAt = now.AddDays(-14) },
            new ProductReview { ProductId = pMi.ProductId, ReviewerId = adminId, ReviewerName = "超级管理员", Action = "approve", Comment = "材料齐全", CreatedAt = now.AddDays(-13) },
            new ProductReview { ProductId = pMac.ProductId, ReviewerId = adminId, ReviewerName = "超级管理员", Action = "submit", CreatedAt = now.AddDays(-19) },
            new ProductReview { ProductId = pMac.ProductId, ReviewerId = adminId, ReviewerName = "超级管理员", Action = "approve", CreatedAt = now.AddDays(-18) },
            new ProductReview { ProductId = pTshirt.ProductId, ReviewerId = adminId, ReviewerName = "超级管理员", Action = "submit", CreatedAt = now.AddDays(-9) },
            new ProductReview { ProductId = pTshirt.ProductId, ReviewerId = adminId, ReviewerName = "超级管理员", Action = "approve", CreatedAt = now.AddDays(-8) },
            new ProductReview { ProductId = pDress.ProductId, ReviewerId = adminId, ReviewerName = "超级管理员", Action = "submit", Comment = "等待审核", CreatedAt = now.AddDays(-4) },
            new ProductReview { ProductId = pSnack.ProductId, ReviewerId = adminId, ReviewerName = "超级管理员", Action = "submit", CreatedAt = now.AddDays(-6) },
            new ProductReview { ProductId = pSnack.ProductId, ReviewerId = adminId, ReviewerName = "超级管理员", Action = "approve", CreatedAt = now.AddDays(-5) }
        );

        db.SaveChanges();
    }
}
