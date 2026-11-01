using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

namespace OctopusOA.Api.Persistence;

public static class OaDbSeeder
{
    public static void Seed(OaDbContext db)
    {
        // P17 后新增 oa_approval_delegate 表，oa_workflow_node 新增 SkipCondition/CcUsersJson 列。
        // 旧 octopus_oa.db 下 EnsureCreated 不会增量补齐，此处自愈避免阻塞 Seed。
        EnsureSchemaUpToDate(db);

        if (!db.SyncUsers.Any())
        {
            SeedMainData(db);
        }
        // 服装行业审批模板：幂等(以 TemplateCode = "garment_production" 为 marker)
        try { SeedGarmentTemplates(db); }
        catch (Exception ex) { Console.WriteLine($"[OaDbSeeder] SeedGarmentTemplates 失败(已跳过): {ex.Message}"); }
    }

    // ── Schema 自愈：补齐缺失的表与列 ─────────────────────────────
    private static void EnsureSchemaUpToDate(OaDbContext db)
    {
        if (!db.Database.IsRelational()) return;

        var conn = db.Database.GetDbConnection();
        var wasOpen = conn.State == System.Data.ConnectionState.Open;
        if (!wasOpen) conn.Open();
        try
        {
            // 1) 表级自愈：oa_approval_delegate(P17 新增) 等
            if (!TableExists(conn, "oa_approval_delegate"))
            {
                var creator = (IRelationalDatabaseCreator)db.GetService<IDatabaseCreator>();
                var script = creator.GenerateCreateScript();
                foreach (var raw in script.Split(';'))
                {
                    var stmt = raw.Trim();
                    if (stmt.Length == 0) continue;
                    if (!stmt.StartsWith("CREATE TABLE", StringComparison.OrdinalIgnoreCase) &&
                        !stmt.StartsWith("CREATE INDEX", StringComparison.OrdinalIgnoreCase) &&
                        !stmt.StartsWith("CREATE UNIQUE INDEX", StringComparison.OrdinalIgnoreCase))
                        continue;
                    var safe = stmt
                        .Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ", StringComparison.OrdinalIgnoreCase)
                        .Replace("CREATE UNIQUE INDEX ", "CREATE UNIQUE INDEX IF NOT EXISTS ", StringComparison.OrdinalIgnoreCase)
                        .Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS ", StringComparison.OrdinalIgnoreCase);
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = safe;
                    try { cmd.ExecuteNonQuery(); }
                    catch (Exception ex) { Console.WriteLine($"[OaDbSeeder] 自愈建表跳过: {ex.Message}"); }
                }
                Console.WriteLine("[OaDbSeeder] Schema 自愈完成（已补齐缺失的表/索引）");
            }

            // 2) 列级自愈：枚举 EF Core 模型，对比每张表的实际列与模型预期列，
            //    缺则 ALTER TABLE ADD COLUMN（按列的 SQL 类型推断 + 主键自增列跳过）
            foreach (var et in db.Model.GetEntityTypes())
            {
                var table = et.GetTableName();
                if (string.IsNullOrEmpty(table)) continue;
                if (!TableExists(conn, table)) continue;

                var existingCols = ReadColumns(conn, table);
                var storeId = StoreObjectIdentifier.Table(table, et.GetSchema());
                foreach (var prop in et.GetProperties())
                {
                    var col = prop.GetColumnName(storeId);
                    if (string.IsNullOrEmpty(col)) continue;
                    if (existingCols.Contains(col)) continue;
                    if (prop.IsPrimaryKey()) continue; // 主键不能用 ALTER TABLE ADD

                    var sqlType = prop.GetColumnType() ?? InferSqliteType(prop.ClrType);
                    var nullable = prop.IsNullable ? "NULL" : "NOT NULL DEFAULT " + DefaultValueLiteral(prop.ClrType);
                    var def = $"{sqlType} {nullable}";
                    try
                    {
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = $"ALTER TABLE \"{table}\" ADD COLUMN \"{col}\" {def}";
                        cmd.ExecuteNonQuery();
                        Console.WriteLine($"[OaDbSeeder] 列级自愈: {table}.{col} 已补齐 ({def})");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[OaDbSeeder] ALTER TABLE 失败 {table}.{col}: {ex.Message}");
                    }
                }
            }
        }
        finally
        {
            if (!wasOpen) conn.Close();
        }
    }

    private static bool TableExists(System.Data.Common.DbConnection conn, string tableName)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name=@n LIMIT 1";
        var p = cmd.CreateParameter();
        p.ParameterName = "@n";
        p.Value = tableName;
        cmd.Parameters.Add(p);
        return cmd.ExecuteScalar() != null;
    }

    private static HashSet<string> ReadColumns(System.Data.Common.DbConnection conn, string tableName)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info(\"{tableName}\")";
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) set.Add(reader.GetString(1));
        return set;
    }

    private static string InferSqliteType(Type clr)
    {
        var t = Nullable.GetUnderlyingType(clr) ?? clr;
        if (t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte) || t == typeof(bool))
            return "INTEGER";
        if (t == typeof(double) || t == typeof(float)) return "REAL";
        if (t == typeof(decimal)) return "decimal(18,4)";
        if (t == typeof(byte[])) return "BLOB";
        return "TEXT";
    }

    private static string DefaultValueLiteral(Type clr)
    {
        var t = Nullable.GetUnderlyingType(clr) ?? clr;
        if (t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte) || t == typeof(bool))
            return "0";
        if (t == typeof(double) || t == typeof(float) || t == typeof(decimal)) return "0";
        if (t == typeof(DateTime)) return "'1970-01-01 00:00:00'";
        return "''";
    }

    private static void SeedMainData(OaDbContext db)
    {
        var now = DateTime.UtcNow;

        // ── 同步用户 ──
        db.SyncUsers.AddRange(
            new SyncUser { Id = 1, UmcUserId = 1, UserName = "admin",    NickName = "超级管理员", Email = "admin@octopus.com",    Status = 1, OaRoles = new(){"oa_admin"}, LastSyncAt = now },
            new SyncUser { Id = 2, UmcUserId = 2, UserName = "zhangsan", NickName = "张三",       Email = "zhangsan@octopus.com", Status = 1, OaRoles = new(){"oa_user"},  LastSyncAt = now },
            new SyncUser { Id = 3, UmcUserId = 3, UserName = "lisi",     NickName = "李四",       Email = "lisi@octopus.com",     Status = 1, OaRoles = new(){"oa_user"},  LastSyncAt = now }
        );

        // ── 部门 ──
        db.OaDepts.AddRange(
            new OaDept { DeptId = 1,  ParentId = 0, DeptName = "章鱼科技有限公司", OrderNum = 0, Status = 1 },
            new OaDept { DeptId = 2,  ParentId = 1, DeptName = "总裁办",           OrderNum = 1, Status = 1 },
            new OaDept { DeptId = 3,  ParentId = 1, DeptName = "技术部",           OrderNum = 2, Status = 1 },
            new OaDept { DeptId = 4,  ParentId = 1, DeptName = "市场部",           OrderNum = 3, Status = 1 },
            new OaDept { DeptId = 5,  ParentId = 1, DeptName = "行政部",           OrderNum = 4, Status = 1 },
            new OaDept { DeptId = 6,  ParentId = 3, DeptName = "前端组",           OrderNum = 1, Status = 1 },
            new OaDept { DeptId = 7,  ParentId = 3, DeptName = "后端组",           OrderNum = 2, Status = 1 },
            new OaDept { DeptId = 8,  ParentId = 3, DeptName = "测试组",           OrderNum = 3, Status = 1 },
            new OaDept { DeptId = 9,  ParentId = 0, DeptName = "海星科技有限公司", OrderNum = 1, Status = 1 },
            new OaDept { DeptId = 10, ParentId = 9, DeptName = "技术部",           OrderNum = 1, Status = 1 },
            new OaDept { DeptId = 11, ParentId = 9, DeptName = "市场部",           OrderNum = 2, Status = 1 }
        );

        db.OaUserDepts.AddRange(
            new OaUserDept { UmcUserId = 1, DeptId = 2,  PostId = 1, PostName = "董事长",   IsPrimary = true },
            new OaUserDept { UmcUserId = 2, DeptId = 3,  PostId = 3, PostName = "技术总监", IsPrimary = true },
            new OaUserDept { UmcUserId = 2, DeptId = 10, PostId = 4, PostName = "工程师",   IsPrimary = false },
            new OaUserDept { UmcUserId = 3, DeptId = 3,  PostId = 4, PostName = "工程师",   IsPrimary = true }
        );

        // ── 审批模板 ──
        db.Templates.AddRange(
            new WorkflowTemplate
            {
                TemplateId = 1, TemplateName = "请假审批", TemplateCode = "leave",
                Description = "员工请假申请流程", Icon = "Calendar", Status = 1,
                FormSchema = """{"fields":[{"key":"leaveType","label":"请假类型","type":"select","options":[{"label":"年假","value":"annual"},{"label":"事假","value":"personal"},{"label":"病假","value":"sick"}],"required":true},{"key":"startDate","label":"开始日期","type":"date","required":true},{"key":"endDate","label":"结束日期","type":"date","required":true},{"key":"days","label":"天数","type":"number","required":true},{"key":"reason","label":"事由","type":"textarea","required":true}]}""",
            },
            new WorkflowTemplate
            {
                TemplateId = 2, TemplateName = "报销审批", TemplateCode = "expense",
                Description = "费用报销申请流程", Icon = "Money", Status = 1,
                FormSchema = """{"fields":[{"key":"expenseType","label":"报销类型","type":"select","options":[{"label":"差旅费","value":"travel"},{"label":"办公用品","value":"office"},{"label":"招待费","value":"entertainment"}],"required":true},{"key":"amount","label":"金额(元)","type":"number","required":true},{"key":"description","label":"说明","type":"textarea","required":true}]}""",
            },
            new WorkflowTemplate
            {
                TemplateId = 3, TemplateName = "补卡审批", TemplateCode = "attendance_fix",
                Description = "忘记打卡后申请补卡", Icon = "Timer", Status = 1,
                FormSchema = """{"fields":[{"key":"fixDate","label":"补卡日期","type":"date","required":true},{"key":"fixType","label":"补卡类型","type":"select","options":[{"label":"上班打卡","value":"checkIn"},{"label":"下班打卡","value":"checkOut"}],"required":true},{"key":"fixTime","label":"补卡时间","type":"text","required":true},{"key":"reason","label":"原因","type":"textarea","required":true}]}""",
            }
        );

        db.Nodes.AddRange(
            new WorkflowNode { NodeId = 1, TemplateId = 1, NodeName = "直属主管审批", NodeOrder = 1, ApproverType = "dept_leader", SlaHours = 24 },
            new WorkflowNode { NodeId = 2, TemplateId = 1, NodeName = "HR 确认",      NodeOrder = 2, ApproverType = "role", ApproverValue = "oa_admin", SlaHours = 48 },
            new WorkflowNode { NodeId = 3, TemplateId = 2, NodeName = "直属主管审批", NodeOrder = 1, ApproverType = "dept_leader", SlaHours = 24 },
            new WorkflowNode { NodeId = 4, TemplateId = 2, NodeName = "财务审核",     NodeOrder = 2, ApproverType = "role", ApproverValue = "oa_admin", SlaHours = 48 },
            new WorkflowNode { NodeId = 5, TemplateId = 2, NodeName = "总经理审批",   NodeOrder = 3, ApproverType = "user", ApproverValue = "1", SlaHours = 72 },
            new WorkflowNode { NodeId = 6, TemplateId = 3, NodeName = "直属主管审批", NodeOrder = 1, ApproverType = "dept_leader", SlaHours = 8 },
            new WorkflowNode { NodeId = 7, TemplateId = 3, NodeName = "HR 确认",      NodeOrder = 2, ApproverType = "role", ApproverValue = "oa_admin", SlaHours = 24 }
        );

        db.Approvals.AddRange(
            new Approval
            {
                ApprovalId = 1, TemplateId = 1, Title = "张三的请假申请",
                ApplicantId = 2, ApplicantName = "张三",
                CurrentNodeOrder = 1, Status = "pending",
                FormData = """{"leaveType":"annual","startDate":"2026-04-10","endDate":"2026-04-11","days":2,"reason":"家中有事需要处理"}""",
            },
            new Approval
            {
                ApprovalId = 2, TemplateId = 2, Title = "李四的报销申请",
                ApplicantId = 3, ApplicantName = "李四",
                CurrentNodeOrder = 3, Status = "approved",
                FormData = """{"expenseType":"travel","amount":3200,"description":"北京出差差旅费"}""",
            },
            new Approval
            {
                ApprovalId = 3, TemplateId = 2, Title = "张三的报销申请",
                ApplicantId = 2, ApplicantName = "张三",
                CurrentNodeOrder = 1, Status = "rejected",
                FormData = """{"expenseType":"office","amount":150,"description":"办公用品采购"}""",
            }
        );

        db.ApprovalRecords.AddRange(
            new ApprovalRecord { RecordId = 1, ApprovalId = 2, NodeId = 3, NodeOrder = 1, ApproverId = 1, ApproverName = "超级管理员", Action = "approve", Comment = "同意",     ActionTime = now.AddDays(-3) },
            new ApprovalRecord { RecordId = 2, ApprovalId = 2, NodeId = 4, NodeOrder = 2, ApproverId = 1, ApproverName = "超级管理员", Action = "approve", Comment = "财务已核实", ActionTime = now.AddDays(-2) },
            new ApprovalRecord { RecordId = 3, ApprovalId = 2, NodeId = 5, NodeOrder = 3, ApproverId = 1, ApproverName = "超级管理员", Action = "approve", Comment = "批准报销",   ActionTime = now.AddDays(-1) },
            new ApprovalRecord { RecordId = 4, ApprovalId = 3, NodeId = 3, NodeOrder = 1, ApproverId = 1, ApproverName = "超级管理员", Action = "reject",  Comment = "金额过低，无需走报销流程", ActionTime = now.AddDays(-1) }
        );

        // ── 职员档案 ──
        db.Employees.AddRange(
            new Employee
            {
                EmployeeId = 1, Status = "temp", Name = "王小明", Gender = "male", Phone = "13600000001",
                BirthDate = "1998-05-15", Email = "wangxm@example.invalid", ApplyPosition = "前端工程师",
                ApplyDeptId = 3, ApplyDeptName = "技术部", Education = "本科", GraduateSchool = "华南理工大学", Major = "软件工程",
                WorkYears = 3, HrRemark = "面试表现良好，技术基础扎实",
                H5Token = "h5_token_001", CreatedBy = 1,
            },
            new Employee
            {
                EmployeeId = 2, Status = "pending", Name = "李小红", Gender = "female", Phone = "13600000002",
                BirthDate = "1996-08-22", Email = "lixh@example.invalid", ApplyPosition = "产品经理",
                ApplyDeptId = 3, ApplyDeptName = "技术部", Education = "硕士", GraduateSchool = "中山大学", Major = "计算机科学",
                WorkYears = 5,
                // ⚠️ 证件号一律用「校验位不成立」的占位值：
                // 校验位合法的号码可能与真实居民身份证重合（原种子值就是校验位正确的
                // 广州天河区号码，等同于泄露真人证件号）。
                // 下面这个值的校验位应为 9 实际给 0 —— 永远不可能是真实证件。
                Photo = "/uploads/photo_lixh.jpg", IdCardNo = "440106199608220000",
                PoliticalStatus = "群众", MaritalStatus = "未婚", NativePlace = "广东广州",
                CurrentAddress = "广州市天河区示例路1号", BankName = "示例银行", BankAccount = "6222000000000000",
                H5Token = "h5_token_002", H5FilledAt = now.AddDays(-1), CreatedBy = 1,
            },
            new Employee
            {
                EmployeeId = 3, Status = "rejected", Name = "赵小强", Gender = "male", Phone = "13600000003",
                ApplyPosition = "测试工程师", ApplyDeptId = 3, ApplyDeptName = "技术部", Education = "大专",
                HrRemark = "学历不符合要求",
                H5Token = "h5_token_003", CreatedBy = 1,
            }
        );

        db.EmployeeEducations.AddRange(
            new EmployeeEducation { Id = 1, EmployeeId = 2, School = "中山大学", Education = "硕士", Major = "计算机科学", StartDate = "2018-09", EndDate = "2021-06", Degree = "硕士", IsFullTime = true },
            new EmployeeEducation { Id = 2, EmployeeId = 2, School = "华南师范大学", Education = "本科", Major = "信息管理", StartDate = "2014-09", EndDate = "2018-06", Degree = "学士", IsFullTime = true }
        );
        db.EmployeeWorkHistories.AddRange(
            new EmployeeWorkHistory { Id = 1, EmployeeId = 2, Company = "腾讯科技", Position = "产品经理", StartDate = "2021-07", EndDate = "2025-12", LeaveReason = "寻求新机会" },
            new EmployeeWorkHistory { Id = 2, EmployeeId = 2, Company = "网易科技", Position = "产品助理", StartDate = "2020-06", EndDate = "2021-06", LeaveReason = "实习结束" }
        );
        db.EmployeeFamilies.AddRange(
            new EmployeeFamily { Id = 1, EmployeeId = 2, Name = "家属甲", Relation = "父亲", Workplace = "示例单位", Phone = "13800001111" },
            new EmployeeFamily { Id = 2, EmployeeId = 2, Name = "家属乙", Relation = "母亲", Workplace = "退休", Phone = "13800002222" }
        );
        db.EmployeeEmergencyContacts.AddRange(
            new EmployeeEmergencyContact { Id = 1, EmployeeId = 2, Name = "家属甲", Relation = "父亲", Phone = "13800001111" }
        );

        // ── 公告 ──
        db.OaNotices.AddRange(
            new OaNotice { NoticeId = 1, Title = "2026年五一劳动节放假通知", NoticeType = "2", Priority = 8,
                Content = "根据国家法定假日规定，公司五一假期为 5月1日至5月5日（共5天），请各位同事提前安排好工作和出行计划。",
                Publisher = "admin", PublishTime = now.AddDays(-3), Source = "umc", Status = 1 },
            new OaNotice { NoticeId = 2, Title = "【紧急】系统维护通知", NoticeType = "3", Priority = 10,
                Content = "OA 系统将于本周六凌晨 2:00-4:00 进行维护升级，期间服务不可用，请合理安排工作。",
                Publisher = "admin", PublishTime = now.AddDays(-1), Source = "umc", Status = 1 },
            new OaNotice { NoticeId = 3, Title = "新员工欢迎 — 欢迎李小红加入", NoticeType = "1", Priority = 3,
                Content = "热烈欢迎李小红入职技术部担任产品经理。请各位同事多多关照新同事。",
                Publisher = "admin", PublishTime = now.AddDays(-5), Source = "umc", Status = 1 },
            new OaNotice { NoticeId = 4, Title = "第二季度 OKR 制定启动", NoticeType = "1", Priority = 5,
                Content = "Q2 OKR 制定工作启动，请各部门负责人于本月底前完成部门目标制定，并汇报给总经理。",
                Publisher = "admin", PublishTime = now.AddDays(-7), Source = "umc", Status = 1 }
        );

        db.OaNoticeReads.Add(new OaNoticeRead { Id = 1, NoticeId = 3, UmcUserId = 1, ReadAt = now.AddDays(-4) });

        // ── 考勤班次 ──
        db.AttendanceRules.AddRange(
            new OaAttendanceRule { Id = 1, Code = "standard", Name = "标准班", WorkStartTime = "09:00", WorkEndTime = "18:00", LateThresholdMin = 15, EarlyLeaveThresholdMin = 30, IsDefault = true,  Status = 1 },
            new OaAttendanceRule { Id = 2, Code = "early",    Name = "早班",   WorkStartTime = "07:00", WorkEndTime = "16:00", LateThresholdMin = 10, EarlyLeaveThresholdMin = 20, IsDefault = false, Status = 1 },
            new OaAttendanceRule { Id = 3, Code = "late",     Name = "晚班",   WorkStartTime = "14:00", WorkEndTime = "23:00", LateThresholdMin = 10, EarlyLeaveThresholdMin = 20, IsDefault = false, Status = 1 },
            new OaAttendanceRule { Id = 4, Code = "flex",     Name = "弹性班", WorkStartTime = "11:00", WorkEndTime = "20:00", LateThresholdMin = 30, EarlyLeaveThresholdMin = 30, IsDefault = false, Status = 1 }
        );

        db.UserShifts.AddRange(
            new OaUserShift { UmcUserId = 1, ShiftId = 1 },
            new OaUserShift { UmcUserId = 2, ShiftId = 4 },
            new OaUserShift { UmcUserId = 3, ShiftId = 1 }
        );

        // 近一周工作日的考勤
        db.Attendances.AddRange(InitAttendances());

        // ── 会议室 ──
        db.MeetingRooms.AddRange(
            new OaMeetingRoom { Id = 1, Name = "星光厅", Capacity = 20, Location = "A 楼 8F", Equipment = """["投影","视频会议","白板"]""", Description = "大型会议室，适合团队会议/客户接待", Status = 1 },
            new OaMeetingRoom { Id = 2, Name = "月光厅", Capacity = 8,  Location = "A 楼 5F", Equipment = """["投影","白板"]""",           Description = "中型会议室，适合 5-8 人讨论",      Status = 1 },
            new OaMeetingRoom { Id = 3, Name = "蓝天小屋", Capacity = 4, Location = "B 楼 3F", Equipment = """["电视","白板"]""",           Description = "小型讨论室，适合 2-4 人一对一",   Status = 1 }
        );

        db.MeetingBookings.AddRange(InitBookings());

        db.SaveChanges();
    }

    // 服装行业：投产/加单/面料采购 三个审批模板（幂等）
    private static void SeedGarmentTemplates(OaDbContext db)
    {
        if (db.Templates.Any(t => t.TemplateCode == "garment_production")) return;

        // 用大 ID 段(50+)避免与主种子冲突
        db.Templates.AddRange(
            new WorkflowTemplate
            {
                TemplateId = 50, TemplateName = "服装款式投产审批", TemplateCode = "garment_production",
                Description = "排程发布前的款式投产审批：设计→技术→生产 三级", Icon = "Box", Status = 1,
                FormSchema = """{"fields":[{"key":"customerName","label":"客户","type":"text","required":true},{"key":"orderNo","label":"客户款号","type":"text","required":true},{"key":"styleCode","label":"款式编码","type":"text","required":true},{"key":"plannedQty","label":"计划数量","type":"number","required":true},{"key":"dueDate","label":"货期","type":"date","required":true},{"key":"team","label":"指派组别","type":"text","required":true}]}""",
            },
            new WorkflowTemplate
            {
                TemplateId = 51, TemplateName = "服装加单审批", TemplateCode = "garment_additional",
                Description = "客户返单/补单：业务→生管→车间", Icon = "Plus", Status = 1,
                FormSchema = """{"fields":[{"key":"originalOrderNo","label":"原款号","type":"text","required":true},{"key":"additionalQty","label":"加单数量","type":"number","required":true},{"key":"reason","label":"加单原因","type":"textarea","required":true}]}""",
            },
            new WorkflowTemplate
            {
                TemplateId = 52, TemplateName = "服装面料采购审批", TemplateCode = "garment_fabric_purchase",
                Description = "面料采购单审批", Icon = "Truck", Status = 1,
                FormSchema = """{"fields":[{"key":"fabricCode","label":"面料编号","type":"text","required":true},{"key":"colorCode","label":"色号","type":"text"},{"key":"qty","label":"采购数量","type":"number","required":true},{"key":"supplier","label":"供应商","type":"text","required":true},{"key":"expectedDate","label":"期望到货日","type":"date","required":true}]}""",
            }
        );

        db.Nodes.AddRange(
            // 投产审批 三级
            new WorkflowNode { NodeId = 50, TemplateId = 50, NodeName = "设计确认", NodeOrder = 1, ApproverType = "role", ApproverValue = "oa_admin", SlaHours = 24 },
            new WorkflowNode { NodeId = 51, TemplateId = 50, NodeName = "技术评估", NodeOrder = 2, ApproverType = "role", ApproverValue = "oa_admin", SlaHours = 24 },
            new WorkflowNode { NodeId = 52, TemplateId = 50, NodeName = "生产确认", NodeOrder = 3, ApproverType = "role", ApproverValue = "oa_admin", SlaHours = 48 },
            // 加单审批 三级
            new WorkflowNode { NodeId = 53, TemplateId = 51, NodeName = "业务确认", NodeOrder = 1, ApproverType = "role", ApproverValue = "oa_admin", SlaHours = 24 },
            new WorkflowNode { NodeId = 54, TemplateId = 51, NodeName = "生管确认", NodeOrder = 2, ApproverType = "role", ApproverValue = "oa_admin", SlaHours = 24 },
            new WorkflowNode { NodeId = 55, TemplateId = 51, NodeName = "车间接单", NodeOrder = 3, ApproverType = "dept_leader", SlaHours = 12 },
            // 面料采购审批 二级
            new WorkflowNode { NodeId = 56, TemplateId = 52, NodeName = "采购主管", NodeOrder = 1, ApproverType = "role", ApproverValue = "oa_admin", SlaHours = 24 },
            new WorkflowNode { NodeId = 57, TemplateId = 52, NodeName = "财务审批", NodeOrder = 2, ApproverType = "role", ApproverValue = "oa_admin", SlaHours = 48 }
        );

        db.SaveChanges();
    }

    private static List<OaAttendance> InitAttendances()
    {
        var list = new List<OaAttendance>();
        long id = 1;
        var today = DateTime.UtcNow.AddHours(8).Date;
        var date = today.AddDays(-7);
        while (date < today)
        {
            var dow = date.DayOfWeek;
            if (dow != DayOfWeek.Saturday && dow != DayOfWeek.Sunday)
            {
                var checkIn = date.AddHours(9).AddMinutes(2).AddHours(-8);
                var checkOut = date.AddHours(18).AddMinutes(15).AddHours(-8);
                list.Add(new OaAttendance
                {
                    Id = id++, UmcUserId = 2, Date = date.ToString("yyyy-MM-dd"),
                    CheckInTime = checkIn, CheckOutTime = checkOut,
                    CheckInStatus = "normal", CheckOutStatus = "normal",
                    WorkHours = 9.2,
                    CheckInIp = "192.168.1.10", CheckOutIp = "192.168.1.10",
                });
                var lisiLate = dow == DayOfWeek.Monday;
                list.Add(new OaAttendance
                {
                    Id = id++, UmcUserId = 3, Date = date.ToString("yyyy-MM-dd"),
                    CheckInTime = date.AddHours(lisiLate ? 9 : 8).AddMinutes(lisiLate ? 30 : 55).AddHours(-8),
                    CheckOutTime = date.AddHours(18).AddMinutes(5).AddHours(-8),
                    CheckInStatus = lisiLate ? "late" : "normal",
                    CheckOutStatus = "normal",
                    WorkHours = 9.2,
                    CheckInIp = "192.168.1.11", CheckOutIp = "192.168.1.11",
                });
            }
            date = date.AddDays(1);
        }
        return list;
    }

    private static List<OaMeetingBooking> InitBookings()
    {
        var now = DateTime.UtcNow;
        var todayBeijing = DateTime.UtcNow.AddHours(8).Date;
        var todayStart = todayBeijing.AddHours(14).AddHours(-8);
        var todayEnd = todayBeijing.AddHours(15).AddMinutes(30).AddHours(-8);
        var tmrStart = todayBeijing.AddDays(1).AddHours(10).AddHours(-8);
        var tmrEnd = todayBeijing.AddDays(1).AddHours(11).AddMinutes(30).AddHours(-8);

        return new List<OaMeetingBooking>
        {
            new()
            {
                Id = 1, RoomId = 1, Title = "Q2 产品规划会",
                UmcUserId = 1, UserName = "超级管理员",
                StartTime = todayStart, EndTime = todayEnd,
                Description = "讨论 Q2 产品路线图和 OKR",
                Attendees = "[1,2,3]", Status = "confirmed", CreateTime = now.AddDays(-1),
            },
            new()
            {
                Id = 2, RoomId = 2, Title = "技术方案评审",
                UmcUserId = 2, UserName = "张三",
                StartTime = tmrStart, EndTime = tmrEnd,
                Description = "评审审批流引擎的技术方案",
                Attendees = "[2,3]", Status = "confirmed", CreateTime = now.AddDays(-1),
            },
            new()
            {
                Id = 3, RoomId = 3, Title = "1v1 同步",
                UmcUserId = 3, UserName = "李四",
                StartTime = todayBeijing.AddDays(2).AddHours(15).AddHours(-8),
                EndTime = todayBeijing.AddDays(2).AddHours(16).AddHours(-8),
                Description = "周度 1v1",
                Attendees = "[3,1]", Status = "confirmed", CreateTime = now,
            },
        };
    }
}
