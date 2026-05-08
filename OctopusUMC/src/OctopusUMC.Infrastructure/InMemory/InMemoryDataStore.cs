using OctopusUMC.Core.Domain.Entities;

namespace OctopusUMC.Infrastructure.InMemory;

public class InMemoryDataStore
{
    // ── 用户 ──────────────────────────────────────────────
    public List<User> Users { get; } = new()
    {
        new User { UserId=1, UserName="admin",   NickName="超级管理员", Email="admin@octopus.com",   PhoneNumber="13800000001", Sex="1", Status=1, PasswordHash=BCrypt.Net.BCrypt.HashPassword("Admin@123"),  CreateTime=DateTime.UtcNow.AddDays(-60) },
        new User { UserId=2, UserName="zhangsan", NickName="张三",     Email="zhangsan@octopus.com", PhoneNumber="13800000002", Sex="1", Status=1, PasswordHash=BCrypt.Net.BCrypt.HashPassword("User@123"),   CreateTime=DateTime.UtcNow.AddDays(-30) },
        new User { UserId=3, UserName="lisi",     NickName="李四",     Email="lisi@octopus.com",     PhoneNumber="13800000003", Sex="0", Status=1, PasswordHash=BCrypt.Net.BCrypt.HashPassword("User@123"),   CreateTime=DateTime.UtcNow.AddDays(-20) },
        new User { UserId=4, UserName="wangwu",   NickName="王五",     Email="wangwu@octopus.com",   PhoneNumber=null,          Sex="1", Status=0, PasswordHash=BCrypt.Net.BCrypt.HashPassword("User@123"),   CreateTime=DateTime.UtcNow.AddDays(-10) },
        new User { UserId=5, UserName="editor",   NickName="编辑员",   Email="editor@octopus.com",   PhoneNumber="13800000005", Sex="0", Status=1, PasswordHash=BCrypt.Net.BCrypt.HashPassword("Editor@123"), CreateTime=DateTime.UtcNow.AddDays(-5)  },
    };

    // ── 用户-部门-职位 关联 ────────────────────────────────
    public List<UserDept> UserDepts { get; } = new()
    {
        new UserDept { UserId=1, DeptId=2, PostId=1,    IsPrimary=true },
        new UserDept { UserId=2, DeptId=3, PostId=3,    IsPrimary=true },
        new UserDept { UserId=3, DeptId=3, PostId=4,    IsPrimary=true },
        new UserDept { UserId=4, DeptId=4, PostId=null, IsPrimary=true },
        new UserDept { UserId=5, DeptId=5, PostId=4,    IsPrimary=true },
    };

    // ── 用户-角色 关联 ─────────────────────────────────────
    public List<UserRole> UserRoles { get; } = new()
    {
        new UserRole { UserId=1, RoleId=1 },
        new UserRole { UserId=2, RoleId=2 },
        new UserRole { UserId=3, RoleId=2 },
        new UserRole { UserId=3, RoleId=3 },
        new UserRole { UserId=4, RoleId=3 },
        new UserRole { UserId=5, RoleId=3 },
    };

    // ── 部门 ──────────────────────────────────────────────
    public List<Dept> Depts { get; } = new()
    {
        new Dept { DeptId=1, ParentId=0, DeptName="章鱼科技有限公司", OrderNum=0, Status=1 },
        new Dept { DeptId=2, ParentId=1, DeptName="总裁办",           OrderNum=1, Status=1 },
        new Dept { DeptId=3, ParentId=1, DeptName="技术部",           OrderNum=2, Status=1 },
        new Dept { DeptId=4, ParentId=1, DeptName="市场部",           OrderNum=3, Status=1 },
        new Dept { DeptId=5, ParentId=1, DeptName="行政部",           OrderNum=4, Status=1 },
        new Dept { DeptId=6, ParentId=3, DeptName="前端组",           OrderNum=1, Status=1 },
        new Dept { DeptId=7, ParentId=3, DeptName="后端组",           OrderNum=2, Status=1 },
        new Dept { DeptId=8, ParentId=3, DeptName="测试组",           OrderNum=3, Status=1 },
    };

    // ── 角色 ──────────────────────────────────────────────
    public List<Role> Roles { get; } = new()
    {
        new Role { RoleId=1, RoleName="超级管理员", RoleKey="admin",  RoleSort=1, DataScope="1", Status=1 },
        new Role { RoleId=2, RoleName="普通用户",   RoleKey="common", RoleSort=2, DataScope="4", Status=1 },
        new Role { RoleId=3, RoleName="编辑员",     RoleKey="editor", RoleSort=3, DataScope="3", Status=1 },
    };

    // ── 角色-菜单 关联 ─────────────────────────────────────
    public List<RoleMenu> RoleMenus { get; } = new()
    {
        // 超级管理员(RoleId=1)
        new RoleMenu { RoleId=1, MenuId=1   }, new RoleMenu { RoleId=1, MenuId=2   }, new RoleMenu { RoleId=1, MenuId=3   },
        new RoleMenu { RoleId=1, MenuId=4   }, new RoleMenu { RoleId=1, MenuId=10  }, new RoleMenu { RoleId=1, MenuId=11  },
        new RoleMenu { RoleId=1, MenuId=12  }, new RoleMenu { RoleId=1, MenuId=100 }, new RoleMenu { RoleId=1, MenuId=101 },
        new RoleMenu { RoleId=1, MenuId=102 }, new RoleMenu { RoleId=1, MenuId=103 }, new RoleMenu { RoleId=1, MenuId=104 },
        new RoleMenu { RoleId=1, MenuId=105 }, new RoleMenu { RoleId=1, MenuId=110 }, new RoleMenu { RoleId=1, MenuId=111 },
        new RoleMenu { RoleId=1, MenuId=112 }, new RoleMenu { RoleId=1, MenuId=113 }, new RoleMenu { RoleId=1, MenuId=120 },
        new RoleMenu { RoleId=1, MenuId=121 }, new RoleMenu { RoleId=1, MenuId=122 }, new RoleMenu { RoleId=1, MenuId=123 },
        new RoleMenu { RoleId=1, MenuId=124 }, new RoleMenu { RoleId=1, MenuId=125 }, new RoleMenu { RoleId=1, MenuId=1001},
        new RoleMenu { RoleId=1, MenuId=1002}, new RoleMenu { RoleId=1, MenuId=1003}, new RoleMenu { RoleId=1, MenuId=1004},
        new RoleMenu { RoleId=1, MenuId=1005}, new RoleMenu { RoleId=1, MenuId=1041}, new RoleMenu { RoleId=1, MenuId=1042},
        new RoleMenu { RoleId=1, MenuId=1043}, new RoleMenu { RoleId=1, MenuId=1044},
        // 普通用户(RoleId=2)
        new RoleMenu { RoleId=2, MenuId=1 }, new RoleMenu { RoleId=2, MenuId=2  }, new RoleMenu { RoleId=2, MenuId=3  },
        new RoleMenu { RoleId=2, MenuId=10}, new RoleMenu { RoleId=2, MenuId=100}, new RoleMenu { RoleId=2, MenuId=101}, new RoleMenu { RoleId=2, MenuId=102},
        // 编辑员(RoleId=3)
        new RoleMenu { RoleId=3, MenuId=1 }, new RoleMenu { RoleId=3, MenuId=2  }, new RoleMenu { RoleId=3, MenuId=3  },
        new RoleMenu { RoleId=3, MenuId=10}, new RoleMenu { RoleId=3, MenuId=100}, new RoleMenu { RoleId=3, MenuId=101},
        new RoleMenu { RoleId=3, MenuId=102}, new RoleMenu { RoleId=3, MenuId=103},
    };

    // ── 菜单 ──────────────────────────────────────────────
    public List<Menu> Menus { get; } = new()
    {
        // 一级目录
        new Menu { MenuId=1,   ParentId=0, MenuName="工作台",   MenuType="M", Path="dashboard",  Icon="HomeFilled",  OrderNum=1,  Status=1, Visible=true },
        new Menu { MenuId=2,   ParentId=0, MenuName="系统管理", MenuType="M", Path="system",     Icon="Setting",     OrderNum=2,  Status=1, Visible=true },
        new Menu { MenuId=3,   ParentId=0, MenuName="系统监控", MenuType="M", Path="monitor",    Icon="Monitor",     OrderNum=3,  Status=1, Visible=true },
        new Menu { MenuId=4,   ParentId=0, MenuName="系统工具", MenuType="M", Path="tool",       Icon="Tools",       OrderNum=4,  Status=1, Visible=true },
        // 工作台菜单
        new Menu { MenuId=10,  ParentId=1, MenuName="工作台首页", MenuType="C", Path="workbench",   Component="dashboard/workbench/index",  OrderNum=1, Status=1, Visible=true },
        new Menu { MenuId=11,  ParentId=1, MenuName="数据分析",   MenuType="C", Path="analysis",    Component="dashboard/analysis/index",   OrderNum=2, Status=1, Visible=true },
        new Menu { MenuId=12,  ParentId=1, MenuName="统计报表",   MenuType="C", Path="statistics",  Component="dashboard/statistics/index", OrderNum=3, Status=1, Visible=true },
        // 系统管理菜单
        new Menu { MenuId=100, ParentId=2, MenuName="用户管理", MenuType="C", Path="user",   Component="system/user/index",   Permission="system:user:list",  Icon="User",      OrderNum=1, Status=1, Visible=true },
        new Menu { MenuId=101, ParentId=2, MenuName="机构管理", MenuType="C", Path="dept",   Component="system/dept/index",   Permission="system:dept:list",  Icon="OfficeBuilding", OrderNum=2, Status=1, Visible=true },
        new Menu { MenuId=102, ParentId=2, MenuName="职位管理", MenuType="C", Path="post",   Component="system/post/index",   Permission="system:post:list",  Icon="Briefcase", OrderNum=3, Status=1, Visible=true },
        new Menu { MenuId=103, ParentId=2, MenuName="菜单管理", MenuType="C", Path="menu",   Component="system/menu/index",   Permission="system:menu:list",  Icon="Menu",      OrderNum=4, Status=1, Visible=true },
        new Menu { MenuId=104, ParentId=2, MenuName="角色管理", MenuType="C", Path="role",   Component="system/role/index",   Permission="system:role:list",  Icon="UserFilled",OrderNum=5, Status=1, Visible=true },
        new Menu { MenuId=105, ParentId=2, MenuName="字典管理", MenuType="C", Path="dict",   Component="system/dict/index",   Permission="system:dict:list",  Icon="Collection",OrderNum=6, Status=1, Visible=true },
        // 用户管理按钮
        new Menu { MenuId=1001, ParentId=100, MenuName="用户查询", MenuType="F", Path="", Permission="system:user:list",      OrderNum=1, Status=1, Visible=false },
        new Menu { MenuId=1002, ParentId=100, MenuName="用户新增", MenuType="F", Path="", Permission="system:user:add",       OrderNum=2, Status=1, Visible=false },
        new Menu { MenuId=1003, ParentId=100, MenuName="用户修改", MenuType="F", Path="", Permission="system:user:edit",      OrderNum=3, Status=1, Visible=false },
        new Menu { MenuId=1004, ParentId=100, MenuName="用户删除", MenuType="F", Path="", Permission="system:user:delete",    OrderNum=4, Status=1, Visible=false },
        new Menu { MenuId=1005, ParentId=100, MenuName="重置密码", MenuType="F", Path="", Permission="system:user:resetPwd",  OrderNum=5, Status=1, Visible=false },
        // 角色管理按钮
        new Menu { MenuId=1041, ParentId=104, MenuName="角色查询", MenuType="F", Path="", Permission="system:role:list",      OrderNum=1, Status=1, Visible=false },
        new Menu { MenuId=1042, ParentId=104, MenuName="角色新增", MenuType="F", Path="", Permission="system:role:add",       OrderNum=2, Status=1, Visible=false },
        new Menu { MenuId=1043, ParentId=104, MenuName="角色修改", MenuType="F", Path="", Permission="system:role:edit",      OrderNum=3, Status=1, Visible=false },
        new Menu { MenuId=1044, ParentId=104, MenuName="角色删除", MenuType="F", Path="", Permission="system:role:delete",    OrderNum=4, Status=1, Visible=false },
        // 系统监控菜单
        new Menu { MenuId=110, ParentId=3, MenuName="在线用户", MenuType="C", Path="online",     Component="monitor/online/index",     Permission="monitor:online:list",     Icon="Connection",      OrderNum=1, Status=1, Visible=true },
        new Menu { MenuId=111, ParentId=3, MenuName="服务监控", MenuType="C", Path="server",     Component="monitor/server/index",     Permission="monitor:server:list",     Icon="Cpu",             OrderNum=2, Status=1, Visible=true },
        new Menu { MenuId=112, ParentId=3, MenuName="操作日志", MenuType="C", Path="operlog",    Component="monitor/operlog/index",    Permission="monitor:operlog:list",    Icon="Document",        OrderNum=3, Status=1, Visible=true },
        new Menu { MenuId=113, ParentId=3, MenuName="访问日志", MenuType="C", Path="logininfor", Component="monitor/logininfor/index", Permission="monitor:logininfor:list", Icon="DocumentChecked", OrderNum=4, Status=1, Visible=true },
        // 系统工具菜单
        new Menu { MenuId=120, ParentId=4, MenuName="公告管理", MenuType="C", Path="notice", Component="tool/notice/index", Permission="tool:notice:list",  Icon="Bell",         OrderNum=1, Status=1, Visible=true },
        new Menu { MenuId=121, ParentId=4, MenuName="文件管理", MenuType="C", Path="file",   Component="tool/file/index",   Permission="tool:file:list",    Icon="FolderOpened", OrderNum=2, Status=1, Visible=true },
        new Menu { MenuId=122, ParentId=4, MenuName="任务调度", MenuType="C", Path="job",    Component="tool/job/index",    Permission="tool:job:list",     Icon="Timer",        OrderNum=3, Status=1, Visible=true },
        new Menu { MenuId=123, ParentId=4, MenuName="系统配置", MenuType="C", Path="config", Component="tool/config/index", Permission="tool:config:list",  Icon="SetUp",        OrderNum=4, Status=1, Visible=true },
        new Menu { MenuId=124, ParentId=4, MenuName="邮件短信", MenuType="C", Path="mail",   Component="tool/mail/index",   Permission="tool:mail:send",    Icon="Message",      OrderNum=5, Status=1, Visible=true },
        new Menu { MenuId=125, ParentId=4, MenuName="接入应用", MenuType="C", Path="client", Component="tool/client/index", Permission="tool:client:list",  Icon="Connection",   OrderNum=6, Status=1, Visible=true },
    };

    // ── 职位 ──────────────────────────────────────────────
    public List<Post> Posts { get; } = new()
    {
        new Post { PostId=1, PostName="董事长",   PostCode="ceo",  PostSort=1, Status=1 },
        new Post { PostId=2, PostName="总经理",   PostCode="gm",   PostSort=2, Status=1 },
        new Post { PostId=3, PostName="技术总监", PostCode="cto",  PostSort=3, Status=1 },
        new Post { PostId=4, PostName="工程师",   PostCode="dev",  PostSort=4, Status=1 },
    };

    // ── 字典 ──────────────────────────────────────────────
    public List<DictType> DictTypes { get; } = new()
    {
        new DictType { DictId=1, DictName="用户性别", DictTypeCode="sys_user_sex",       Status=1, Remark="用户性别列表" },
        new DictType { DictId=2, DictName="菜单状态", DictTypeCode="sys_show_hide",      Status=1, Remark="菜单状态列表" },
        new DictType { DictId=3, DictName="系统开关", DictTypeCode="sys_normal_disable", Status=1, Remark="系统开关列表" },
    };

    public List<DictData> DictDatas { get; } = new()
    {
        new DictData { DictCode=1, DictType="sys_user_sex",       DictLabel="男",   DictValue="1", DictSort=1, Status=1, IsDefault=true  },
        new DictData { DictCode=2, DictType="sys_user_sex",       DictLabel="女",   DictValue="0", DictSort=2, Status=1, IsDefault=false },
        new DictData { DictCode=3, DictType="sys_user_sex",       DictLabel="未知", DictValue="2", DictSort=3, Status=1, IsDefault=false },
        new DictData { DictCode=4, DictType="sys_show_hide",      DictLabel="显示", DictValue="1", DictSort=1, Status=1, IsDefault=true  },
        new DictData { DictCode=5, DictType="sys_show_hide",      DictLabel="隐藏", DictValue="0", DictSort=2, Status=1, IsDefault=false },
        new DictData { DictCode=6, DictType="sys_normal_disable", DictLabel="正常", DictValue="1", DictSort=1, Status=1, IsDefault=true  },
        new DictData { DictCode=7, DictType="sys_normal_disable", DictLabel="停用", DictValue="0", DictSort=2, Status=1, IsDefault=false },
    };

    // ── OIDC 客户端 ────────────────────────────────────────
    public List<OidcClient> OidcClients { get; } = new()
    {
        new OidcClient
        {
            Id = "oa-client-001",
            ClientId = "octopus-oa-web",
            ClientName = "OctopusOA Web",
            ClientType = "public",
            RedirectUris = new() { "http://localhost:5174/callback" },
            PostLogoutRedirectUris = new() { "http://localhost:5174" },
            Status = 1,
            CreatedAt = DateTime.UtcNow.AddDays(-30)
        },
        new OidcClient
        {
            Id = "plm-client-001",
            ClientId = "octopus-plm-web",
            ClientName = "OctopusPLM Web",
            ClientType = "public",
            RedirectUris = new() { "http://localhost:5175/callback" },
            PostLogoutRedirectUris = new() { "http://localhost:5175" },
            Status = 1,
            CreatedAt = DateTime.UtcNow.AddDays(-30)
        },
        new OidcClient
        {
            Id = "crm-client-001",
            ClientId = "octopus-crm-web",
            ClientName = "OctopusCRM Web",
            ClientType = "public",
            RedirectUris = new() { "http://localhost:5176/callback" },
            PostLogoutRedirectUris = new() { "http://localhost:5176" },
            Status = 1,
            CreatedAt = DateTime.UtcNow.AddDays(-30)
        }
    };

    // ── 公告 ──────────────────────────────────────────────
    public List<Notice> Notices { get; } = new()
    {
        new Notice { NoticeId=1, NoticeTitle="系统升级通知",   NoticeType="2", NoticeContent="系统将于本周六进行升级维护，请提前做好准备。",           Status=1, CreateBy="admin",    CreateTime=DateTime.UtcNow.AddDays(-2)  },
        new Notice { NoticeId=2, NoticeTitle="国庆节放假通知", NoticeType="1", NoticeContent="国庆节假期为10月1日至7日，共7天，请大家提前安排工作。", Status=1, CreateBy="admin",    CreateTime=DateTime.UtcNow.AddDays(-5)  },
        new Notice { NoticeId=3, NoticeTitle="新员工入职培训", NoticeType="1", NoticeContent="请新员工于下周一上午9点到会议室参加入职培训。",           Status=0, CreateBy="zhangsan", CreateTime=DateTime.UtcNow.AddDays(-10) },
    };

    // ── 任务调度 ───────────────────────────────────────────
    public List<Job> Jobs { get; } = new()
    {
        new Job { JobId=1, JobName="清理过期Token", JobGroup="SYSTEM",  InvokeTarget="CleanExpiredTokensJob", CronExpression="0 0 2 * * ?",   Status=1 },
        new Job { JobId=2, JobName="同步用户缓存",  JobGroup="SYSTEM",  InvokeTarget="SyncUserCacheJob",      CronExpression="0 */30 * * * ?", Status=1 },
        new Job { JobId=3, JobName="生成月度报表",  JobGroup="REPORT",  InvokeTarget="GenerateMonthlyReport", CronExpression="0 0 6 1 * ?",    Status=0 },
    };

    // ── 系统配置 ───────────────────────────────────────────
    public List<SysConfig> Configs { get; } = new()
    {
        new SysConfig { ConfigId=1, ConfigName="系统名称",          ConfigKey="sys.name",         ConfigValue="OctopusUMC", ConfigType=true,  Remark="系统名称配置" },
        new SysConfig { ConfigId=2, ConfigName="用户初始密码",      ConfigKey="sys.user.initPwd", ConfigValue="User@123",   ConfigType=true,  Remark="新增用户初始密码" },
        new SysConfig { ConfigId=3, ConfigName="Token有效期(小时)", ConfigKey="sys.token.expiry", ConfigValue="24",         ConfigType=true,  Remark="Token 有效时长" },
    };

    // ── 文件 ──────────────────────────────────────────────
    public List<OssFile> OssFiles { get; } = new()
    {
        new OssFile { OssId=1, FileName="avatar_admin.png",   OriginalName="头像.png",        FileSuffix=".png",  Url="/uploads/avatar_admin.png",   Service="local", CreateBy="admin", CreateTime=DateTime.UtcNow.AddDays(-10) },
        new OssFile { OssId=2, FileName="report_2024q1.xlsx", OriginalName="2024Q1报表.xlsx", FileSuffix=".xlsx", Url="/uploads/report_2024q1.xlsx", Service="local", CreateBy="admin", CreateTime=DateTime.UtcNow.AddDays(-5)  },
    };

    // ── 操作日志 ───────────────────────────────────────────
    public List<OperLog> OperLogs { get; } = new()
    {
        new OperLog { OperId=1, Title="用户管理", OperName="admin",    OperUrl="/api/system/user",      RequestMethod="POST",   OperParam="{\"userName\":\"test\"}", JsonResult="{\"code\":200}", Status=1, OperTime=DateTime.UtcNow.AddHours(-1), CostTime=35 },
        new OperLog { OperId=2, Title="角色管理", OperName="admin",    OperUrl="/api/system/role/menu", RequestMethod="POST",   OperParam="{\"roleId\":2}",          JsonResult="{\"code\":200}", Status=1, OperTime=DateTime.UtcNow.AddHours(-2), CostTime=22 },
        new OperLog { OperId=3, Title="用户管理", OperName="zhangsan", OperUrl="/api/system/user/3",    RequestMethod="DELETE", OperParam="{\"ids\":\"3\"}",         JsonResult="{\"code\":500}", Status=0, OperTime=DateTime.UtcNow.AddHours(-3), CostTime=5,  ErrorMsg="权限不足" },
        new OperLog { OperId=4, Title="字典管理", OperName="admin",    OperUrl="/api/system/dict/type", RequestMethod="POST",   OperParam="{\"dictType\":\"test\"}", JsonResult="{\"code\":200}", Status=1, OperTime=DateTime.UtcNow.AddHours(-5), CostTime=18 },
        new OperLog { OperId=5, Title="菜单管理", OperName="admin",    OperUrl="/api/system/menu",      RequestMethod="PUT",    OperParam="{\"menuId\":100}",        JsonResult="{\"code\":200}", Status=1, OperTime=DateTime.UtcNow.AddHours(-8), CostTime=41 },
    };

    // ── 访问日志 ───────────────────────────────────────────
    public List<LoginInfo> LoginInfos { get; } = new()
    {
        new LoginInfo { InfoId=1, UserName="admin",    Ipaddr="192.168.1.1",  LoginLocation="内网IP", Browser="Chrome 120",  Os="macOS",   Status=1, Msg="登录成功",       LoginTime=DateTime.UtcNow.AddHours(-1)  },
        new LoginInfo { InfoId=2, UserName="zhangsan", Ipaddr="192.168.1.2",  LoginLocation="内网IP", Browser="Edge 120",    Os="Windows", Status=1, Msg="登录成功",       LoginTime=DateTime.UtcNow.AddHours(-2)  },
        new LoginInfo { InfoId=3, UserName="unknown",  Ipaddr="110.23.45.67", LoginLocation="北京市", Browser="Firefox 121", Os="Linux",   Status=0, Msg="用户名或密码错误", LoginTime=DateTime.UtcNow.AddHours(-3) },
        new LoginInfo { InfoId=4, UserName="lisi",     Ipaddr="192.168.1.3",  LoginLocation="内网IP", Browser="Safari 17",   Os="iOS",     Status=1, Msg="登录成功",       LoginTime=DateTime.UtcNow.AddHours(-6)  },
        new LoginInfo { InfoId=5, UserName="admin",    Ipaddr="192.168.1.1",  LoginLocation="内网IP", Browser="Chrome 120",  Os="macOS",   Status=1, Msg="退出成功",       LoginTime=DateTime.UtcNow.AddHours(-12) },
    };

    // 计数器（生成自增ID用）
    private long _nextUserId   = 6;
    private long _nextDeptId   = 9;
    private long _nextRoleId   = 4;
    private long _nextMenuId   = 2000;
    private long _nextPostId   = 5;
    private long _nextDictId   = 4;
    private long _nextDictCode = 8;
    private long _nextNoticeId = 4;
    private long _nextJobId    = 4;
    private long _nextConfigId = 4;
    private long _nextOssId    = 3;
    private long _nextOperId   = 6;
    private long _nextInfoId   = 6;

    public long NextUserId()   => Interlocked.Increment(ref _nextUserId);
    public long NextDeptId()   => Interlocked.Increment(ref _nextDeptId);
    public long NextRoleId()   => Interlocked.Increment(ref _nextRoleId);
    public long NextMenuId()   => Interlocked.Increment(ref _nextMenuId);
    public long NextPostId()   => Interlocked.Increment(ref _nextPostId);
    public long NextDictId()   => Interlocked.Increment(ref _nextDictId);
    public long NextDictCode() => Interlocked.Increment(ref _nextDictCode);
    public long NextNoticeId() => Interlocked.Increment(ref _nextNoticeId);
    public long NextJobId()    => Interlocked.Increment(ref _nextJobId);
    public long NextConfigId() => Interlocked.Increment(ref _nextConfigId);
    public long NextOssId()    => Interlocked.Increment(ref _nextOssId);
    public long NextOperId()   => Interlocked.Increment(ref _nextOperId);
    public long NextInfoId()   => Interlocked.Increment(ref _nextInfoId);
}
