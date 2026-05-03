using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;

namespace OctopusOA.Api.Persistence;

public class OaDbContext : DbContext
{
    public OaDbContext(DbContextOptions<OaDbContext> options) : base(options) { }

    // ── 用户/部门 ──
    public DbSet<SyncUser> SyncUsers => Set<SyncUser>();
    public DbSet<OaDept> OaDepts => Set<OaDept>();
    public DbSet<OaUserDept> OaUserDepts => Set<OaUserDept>();

    // ── 审批 ──
    public DbSet<WorkflowTemplate> Templates => Set<WorkflowTemplate>();
    public DbSet<WorkflowNode> Nodes => Set<WorkflowNode>();
    public DbSet<Approval> Approvals => Set<Approval>();
    public DbSet<ApprovalRecord> ApprovalRecords => Set<ApprovalRecord>();

    // ── 职员档案 ──
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmployeeEducation> EmployeeEducations => Set<EmployeeEducation>();
    public DbSet<EmployeeWorkHistory> EmployeeWorkHistories => Set<EmployeeWorkHistory>();
    public DbSet<EmployeeFamily> EmployeeFamilies => Set<EmployeeFamily>();
    public DbSet<EmployeeEmergencyContact> EmployeeEmergencyContacts => Set<EmployeeEmergencyContact>();

    // ── 公告 ──
    public DbSet<OaNotice> OaNotices => Set<OaNotice>();
    public DbSet<OaNoticeRead> OaNoticeReads => Set<OaNoticeRead>();

    // ── 考勤 ──
    public DbSet<OaAttendanceRule> AttendanceRules => Set<OaAttendanceRule>();
    public DbSet<OaUserShift> UserShifts => Set<OaUserShift>();
    public DbSet<OaAttendance> Attendances => Set<OaAttendance>();
    public DbSet<OaAttendanceFix> AttendanceFixes => Set<OaAttendanceFix>();

    // ── 会议室 ──
    public DbSet<OaMeetingRoom> MeetingRooms => Set<OaMeetingRoom>();
    public DbSet<OaMeetingBooking> MeetingBookings => Set<OaMeetingBooking>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // List<string> / List<long> → JSON TEXT ValueConverters
        var stringListConv = new ValueConverter<List<string>, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new());
        var stringListComp = new ValueComparer<List<string>>(
            (l, r) => l != null && r != null && l.SequenceEqual(r),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v == null ? 0 : v.GetHashCode())),
            c => c.ToList());

        // ── SyncUser ──
        b.Entity<SyncUser>(e =>
        {
            e.ToTable("oa_sync_user");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.OaRoles).HasConversion(stringListConv, stringListComp).HasColumnType("TEXT");
        });

        b.Entity<OaDept>(e =>
        {
            e.ToTable("oa_dept");
            e.HasKey(x => x.DeptId);
            e.Property(x => x.DeptId).ValueGeneratedNever(); // DeptId 跟 UMC 对齐，不自增
        });

        b.Entity<OaUserDept>(e =>
        {
            e.ToTable("oa_user_dept");
            e.HasKey(x => new { x.UmcUserId, x.DeptId });
        });

        b.Entity<WorkflowTemplate>(e =>
        {
            e.ToTable("oa_workflow_template");
            e.HasKey(x => x.TemplateId);
            e.Property(x => x.TemplateId).ValueGeneratedOnAdd();
        });

        b.Entity<WorkflowNode>(e =>
        {
            e.ToTable("oa_workflow_node");
            e.HasKey(x => x.NodeId);
            e.Property(x => x.NodeId).ValueGeneratedOnAdd();
        });

        b.Entity<Approval>(e =>
        {
            e.ToTable("oa_approval");
            e.HasKey(x => x.ApprovalId);
            e.Property(x => x.ApprovalId).ValueGeneratedOnAdd();
        });

        b.Entity<ApprovalRecord>(e =>
        {
            e.ToTable("oa_approval_record");
            e.HasKey(x => x.RecordId);
            e.Property(x => x.RecordId).ValueGeneratedOnAdd();
        });

        b.Entity<Employee>(e =>
        {
            e.ToTable("oa_employee");
            e.HasKey(x => x.EmployeeId);
            e.Property(x => x.EmployeeId).ValueGeneratedOnAdd();
        });

        b.Entity<EmployeeEducation>(e => { e.ToTable("oa_employee_education"); e.HasKey(x => x.Id); e.Property(x => x.Id).ValueGeneratedOnAdd(); });
        b.Entity<EmployeeWorkHistory>(e => { e.ToTable("oa_employee_work"); e.HasKey(x => x.Id); e.Property(x => x.Id).ValueGeneratedOnAdd(); });
        b.Entity<EmployeeFamily>(e => { e.ToTable("oa_employee_family"); e.HasKey(x => x.Id); e.Property(x => x.Id).ValueGeneratedOnAdd(); });
        b.Entity<EmployeeEmergencyContact>(e => { e.ToTable("oa_employee_emergency"); e.HasKey(x => x.Id); e.Property(x => x.Id).ValueGeneratedOnAdd(); });

        b.Entity<OaNotice>(e =>
        {
            e.ToTable("oa_notice");
            e.HasKey(x => x.NoticeId);
            e.Property(x => x.NoticeId).ValueGeneratedOnAdd();
        });

        b.Entity<OaNoticeRead>(e =>
        {
            e.ToTable("oa_notice_read");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
        });

        b.Entity<OaAttendanceRule>(e =>
        {
            e.ToTable("oa_attendance_rule");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
        });

        b.Entity<OaUserShift>(e =>
        {
            e.ToTable("oa_user_shift");
            e.HasKey(x => x.UmcUserId);
        });

        b.Entity<OaAttendance>(e =>
        {
            e.ToTable("oa_attendance");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.HasIndex(x => new { x.UmcUserId, x.Date }).IsUnique();
        });

        b.Entity<OaAttendanceFix>(e =>
        {
            e.ToTable("oa_attendance_fix");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
        });

        b.Entity<OaMeetingRoom>(e =>
        {
            e.ToTable("oa_meeting_room");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
        });

        b.Entity<OaMeetingBooking>(e =>
        {
            e.ToTable("oa_meeting_booking");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
        });
    }
}
