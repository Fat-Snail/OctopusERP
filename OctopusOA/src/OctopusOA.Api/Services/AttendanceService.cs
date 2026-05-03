using System.Text.Json;
using OctopusOA.Api.DTOs;
using OctopusOA.Api.Persistence;

namespace OctopusOA.Api.Services;

/// <summary>考勤服务：打卡、状态计算、补卡联动</summary>
public class AttendanceService
{
    private readonly OaDbContext _db;

    public AttendanceService(OaDbContext db) => _db = db;

    /// <summary>向后兼容：返回默认班次</summary>
    public OaAttendanceRule GetRule() => GetDefaultShift();

    public OaAttendanceRule GetDefaultShift() =>
        _db.AttendanceRules.FirstOrDefault(r => r.IsDefault && r.Status == 1)
        ?? _db.AttendanceRules.First(r => r.Status == 1);

    /// <summary>按用户查班次（未配置则用默认）</summary>
    public OaAttendanceRule GetShiftByUser(long umcUserId)
    {
        var us = _db.UserShifts.FirstOrDefault(u => u.UmcUserId == umcUserId);
        if (us != null)
        {
            var s = _db.AttendanceRules.FirstOrDefault(r => r.Id == us.ShiftId && r.Status == 1);
            if (s != null) return s;
        }
        return GetDefaultShift();
    }

    public List<OaAttendanceRule> GetAllShifts() =>
        _db.AttendanceRules.OrderBy(r => r.Id).ToList();

    public (OaAttendanceRule? shift, string? error) CreateShift(AttendanceRuleDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Code)) return (null, "班次编码不能为空");
        if (_db.AttendanceRules.Any(r => r.Code == dto.Code)) return (null, "班次编码已存在");

        var shift = new OaAttendanceRule
        {
            Code = dto.Code, Name = dto.Name,
            WorkStartTime = dto.WorkStartTime, WorkEndTime = dto.WorkEndTime,
            LateThresholdMin = dto.LateThresholdMin,
            EarlyLeaveThresholdMin = dto.EarlyLeaveThresholdMin,
            IpWhiteList = dto.IpWhiteList,
            IsDefault = false,
            Status = dto.Status,
        };
        _db.AttendanceRules.Add(shift);
        _db.SaveChanges();
        return (shift, null);
    }

    public string? UpdateShift(AttendanceRuleDto dto)
    {
        var shift = _db.AttendanceRules.FirstOrDefault(r => r.Id == dto.Id);
        if (shift == null) return "班次不存在";
        shift.Name = dto.Name;
        shift.WorkStartTime = dto.WorkStartTime;
        shift.WorkEndTime = dto.WorkEndTime;
        shift.LateThresholdMin = dto.LateThresholdMin;
        shift.EarlyLeaveThresholdMin = dto.EarlyLeaveThresholdMin;
        shift.IpWhiteList = dto.IpWhiteList;
        shift.Status = dto.Status;
        _db.SaveChanges();
        return null;
    }

    public string? DeleteShift(long id)
    {
        var shift = _db.AttendanceRules.FirstOrDefault(r => r.Id == id);
        if (shift == null) return "班次不存在";
        if (shift.IsDefault) return "默认班次不可删除";
        if (_db.UserShifts.Any(us => us.ShiftId == id)) return "该班次已分配给用户，不可删除";
        _db.AttendanceRules.Remove(shift);
        _db.SaveChanges();
        return null;
    }

    public string? SetDefaultShift(long id)
    {
        var shift = _db.AttendanceRules.FirstOrDefault(r => r.Id == id);
        if (shift == null) return "班次不存在";
        foreach (var s in _db.AttendanceRules) s.IsDefault = false;
        shift.IsDefault = true;
        _db.SaveChanges();
        return null;
    }

    public void UpdateRule(AttendanceRuleDto dto)
    {
        var rule = GetDefaultShift();
        rule.Name = dto.Name;
        rule.WorkStartTime = dto.WorkStartTime;
        rule.WorkEndTime = dto.WorkEndTime;
        rule.LateThresholdMin = dto.LateThresholdMin;
        rule.EarlyLeaveThresholdMin = dto.EarlyLeaveThresholdMin;
        rule.IpWhiteList = dto.IpWhiteList;
        _db.SaveChanges();
    }

    public List<UserShiftItem> GetAllUserShifts()
    {
        return _db.SyncUsers.ToList().Select(u =>
        {
            var shift = GetShiftByUser(u.UmcUserId);
            return new UserShiftItem
            {
                UmcUserId = u.UmcUserId, UserName = u.UserName, NickName = u.NickName,
                ShiftId = shift.Id, ShiftName = shift.Name, ShiftCode = shift.Code,
                WorkStartTime = shift.WorkStartTime, WorkEndTime = shift.WorkEndTime,
            };
        }).ToList();
    }

    public string? AssignUserShift(long umcUserId, long shiftId)
    {
        if (!_db.AttendanceRules.Any(r => r.Id == shiftId))
            return "班次不存在";

        var existing = _db.UserShifts.FirstOrDefault(u => u.UmcUserId == umcUserId);
        if (existing != null)
        {
            existing.ShiftId = shiftId;
            existing.AssignedAt = DateTime.UtcNow;
        }
        else
        {
            _db.UserShifts.Add(new OaUserShift
            {
                UmcUserId = umcUserId, ShiftId = shiftId, AssignedAt = DateTime.UtcNow,
            });
        }
        _db.SaveChanges();
        return null;
    }

    private static string BeijingToday() => DateTime.UtcNow.AddHours(8).ToString("yyyy-MM-dd");
    private static DateTime BeijingNow() => DateTime.UtcNow.AddHours(8);

    public (OaAttendance? attendance, string? error) CheckIn(long userId, string? ip)
    {
        var rule = GetShiftByUser(userId);
        if (!IsIpAllowed(ip, rule)) return (null, "当前 IP 不在打卡白名单内");

        var today = BeijingToday();
        var record = _db.Attendances.FirstOrDefault(a => a.UmcUserId == userId && a.Date == today);
        if (record != null && record.CheckInTime.HasValue)
            return (null, "今日已打过上班卡");

        var nowUtc = DateTime.UtcNow;
        var nowBeijing = BeijingNow();
        var status = ComputeCheckInStatus(nowBeijing, rule);

        if (record == null)
        {
            record = new OaAttendance
            {
                UmcUserId = userId,
                Date = today,
                CheckInTime = nowUtc,
                CheckInStatus = status,
                CheckInIp = ip,
                CreateTime = nowUtc,
            };
            _db.Attendances.Add(record);
        }
        else
        {
            record.CheckInTime = nowUtc;
            record.CheckInStatus = status;
            record.CheckInIp = ip;
        }

        _db.SaveChanges();
        return (record, null);
    }

    public (OaAttendance? attendance, string? error) CheckOut(long userId, string? ip)
    {
        var rule = GetShiftByUser(userId);
        if (!IsIpAllowed(ip, rule)) return (null, "当前 IP 不在打卡白名单内");

        var today = BeijingToday();
        var record = _db.Attendances.FirstOrDefault(a => a.UmcUserId == userId && a.Date == today);
        if (record == null) return (null, "尚未打上班卡");
        if (record.CheckOutTime.HasValue) return (null, "今日已打过下班卡");

        var nowUtc = DateTime.UtcNow;
        var nowBeijing = BeijingNow();
        record.CheckOutTime = nowUtc;
        record.CheckOutStatus = ComputeCheckOutStatus(nowBeijing, rule);
        record.CheckOutIp = ip;

        if (record.CheckInTime.HasValue)
            record.WorkHours = Math.Round((nowUtc - record.CheckInTime.Value).TotalHours, 2);

        _db.SaveChanges();
        return (record, null);
    }

    public TodayAttendanceResponse GetToday(long userId)
    {
        var rule = GetShiftByUser(userId);
        var today = BeijingToday();
        var record = _db.Attendances.FirstOrDefault(a => a.UmcUserId == userId && a.Date == today);

        return new TodayAttendanceResponse
        {
            Date = today,
            CheckInTime = record?.CheckInTime,
            CheckOutTime = record?.CheckOutTime,
            CheckInStatus = record?.CheckInStatus ?? "missing",
            CheckOutStatus = record?.CheckOutStatus ?? "missing",
            CanCheckIn = record?.CheckInTime == null,
            CanCheckOut = record?.CheckInTime != null && record?.CheckOutTime == null,
            RuleWorkStart = rule.WorkStartTime,
            RuleWorkEnd = rule.WorkEndTime,
        };
    }

    public List<AttendanceItem> GetMine(long userId, string? month)
    {
        var prefix = month ?? BeijingNow().ToString("yyyy-MM");
        var records = _db.Attendances
            .Where(a => a.UmcUserId == userId && a.Date.StartsWith(prefix))
            .OrderBy(a => a.Date)
            .ToList();

        var fixIds = _db.AttendanceFixes.Select(f => f.AttendanceId).ToList().ToHashSet();

        var items = new List<AttendanceItem>();
        var (startDate, endDate) = ParseMonthRange(prefix);
        for (var d = startDate; d <= endDate && d <= BeijingNow().Date; d = d.AddDays(1))
        {
            var dateStr = d.ToString("yyyy-MM-dd");
            var r = records.FirstOrDefault(x => x.Date == dateStr);
            var isWeekend = d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            items.Add(new AttendanceItem
            {
                Id = r?.Id ?? 0,
                UmcUserId = userId,
                Date = dateStr,
                CheckInTime = r?.CheckInTime,
                CheckOutTime = r?.CheckOutTime,
                CheckInStatus = r?.CheckInStatus ?? (isWeekend ? "weekend" : "missing"),
                CheckOutStatus = r?.CheckOutStatus ?? (isWeekend ? "weekend" : "missing"),
                WorkHours = r?.WorkHours ?? 0,
                IsFixed = r != null && fixIds.Contains(r.Id),
            });
        }
        return items;
    }

    public List<AttendanceStatsItem> GetStats(string? month)
    {
        var prefix = month ?? BeijingNow().ToString("yyyy-MM");
        var (startDate, endDate) = ParseMonthRange(prefix);
        var totalWorkDays = 0;
        for (var d = startDate; d <= endDate && d <= BeijingNow().Date; d = d.AddDays(1))
            if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday)
                totalWorkDays++;

        var allRecords = _db.Attendances.Where(a => a.Date.StartsWith(prefix)).ToList();

        return _db.SyncUsers.ToList().Select(u =>
        {
            var records = allRecords.Where(a => a.UmcUserId == u.UmcUserId).ToList();
            var normalDays = records.Count(r => r.CheckInStatus == "normal" && r.CheckOutStatus == "normal");
            var lateCount = records.Count(r => r.CheckInStatus == "late");
            var earlyLeaveCount = records.Count(r => r.CheckOutStatus == "early");
            var missingCount = Math.Max(0, totalWorkDays - records.Count);

            return new AttendanceStatsItem
            {
                UmcUserId = u.UmcUserId,
                UserName = u.UserName,
                NickName = u.NickName,
                NormalDays = normalDays,
                LateCount = lateCount,
                EarlyLeaveCount = earlyLeaveCount,
                MissingCount = missingCount,
                TotalWorkHours = Math.Round(records.Sum(r => r.WorkHours), 2),
            };
        }).ToList();
    }

    public List<AttendanceItem> GetAbnormal(string? month)
    {
        var prefix = month ?? BeijingNow().ToString("yyyy-MM");
        var records = _db.Attendances
            .Where(a => a.Date.StartsWith(prefix) &&
                   (a.CheckInStatus == "late" || a.CheckOutStatus == "early"))
            .OrderByDescending(a => a.Date)
            .ToList();

        var fixIds = _db.AttendanceFixes.Select(f => f.AttendanceId).ToList().ToHashSet();

        return records.Select(r =>
        {
            var u = _db.SyncUsers.FirstOrDefault(x => x.UmcUserId == r.UmcUserId);
            return new AttendanceItem
            {
                Id = r.Id, UmcUserId = r.UmcUserId,
                UserName = u?.UserName ?? "", NickName = u?.NickName ?? "",
                Date = r.Date,
                CheckInTime = r.CheckInTime, CheckOutTime = r.CheckOutTime,
                CheckInStatus = r.CheckInStatus, CheckOutStatus = r.CheckOutStatus,
                WorkHours = r.WorkHours,
                IsFixed = fixIds.Contains(r.Id),
            };
        }).ToList();
    }

    /// <summary>由 ApprovalService 在 attendance_fix 审批通过时调用</summary>
    public void HandleApprovalFix(Approval approval)
    {
        try
        {
            var form = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(approval.FormData);
            if (form == null) return;

            var fixDate = form.TryGetValue("fixDate", out var d) ? d.GetString() : null;
            var fixType = form.TryGetValue("fixType", out var t) ? t.GetString() : null;
            var fixTimeStr = form.TryGetValue("fixTime", out var tm) ? tm.GetString() : null;
            var reason = form.TryGetValue("reason", out var r) ? r.GetString() : null;

            if (string.IsNullOrEmpty(fixDate) || string.IsNullOrEmpty(fixType) || string.IsNullOrEmpty(fixTimeStr)) return;

            var beijingDt = DateTime.Parse($"{fixDate} {fixTimeStr}");
            var utcDt = beijingDt.AddHours(-8);

            var record = _db.Attendances.FirstOrDefault(a => a.UmcUserId == approval.ApplicantId && a.Date == fixDate);
            if (record == null)
            {
                record = new OaAttendance
                {
                    UmcUserId = approval.ApplicantId,
                    Date = fixDate,
                };
                _db.Attendances.Add(record);
                _db.SaveChanges(); // 先落库以拿到 Id
            }

            if (fixType == "checkIn")
            {
                record.CheckInTime = utcDt;
                record.CheckInStatus = "normal";
            }
            else if (fixType == "checkOut")
            {
                record.CheckOutTime = utcDt;
                record.CheckOutStatus = "normal";
            }

            if (record.CheckInTime.HasValue && record.CheckOutTime.HasValue)
                record.WorkHours = Math.Round((record.CheckOutTime.Value - record.CheckInTime.Value).TotalHours, 2);

            _db.AttendanceFixes.Add(new OaAttendanceFix
            {
                AttendanceId = record.Id,
                ApprovalId = approval.ApprovalId,
                Type = fixType,
                FixTime = utcDt,
                Reason = reason,
            });
            _db.SaveChanges();
        }
        catch
        {
            // 解析失败不影响审批主流程
        }
    }

    private static string ComputeCheckInStatus(DateTime nowBeijing, OaAttendanceRule rule)
    {
        var workStart = DateTime.Parse($"{nowBeijing:yyyy-MM-dd} {rule.WorkStartTime}");
        var threshold = workStart.AddMinutes(rule.LateThresholdMin);
        return nowBeijing > threshold ? "late" : "normal";
    }

    private static string ComputeCheckOutStatus(DateTime nowBeijing, OaAttendanceRule rule)
    {
        var workEnd = DateTime.Parse($"{nowBeijing:yyyy-MM-dd} {rule.WorkEndTime}");
        var threshold = workEnd.AddMinutes(-rule.EarlyLeaveThresholdMin);
        return nowBeijing < threshold ? "early" : "normal";
    }

    private static bool IsIpAllowed(string? ip, OaAttendanceRule rule)
    {
        if (string.IsNullOrWhiteSpace(rule.IpWhiteList)) return true;
        if (string.IsNullOrWhiteSpace(ip)) return false;
        return rule.IpWhiteList.Split(',').Select(s => s.Trim()).Contains(ip);
    }

    private static (DateTime start, DateTime end) ParseMonthRange(string prefix)
    {
        var parts = prefix.Split('-');
        var year = int.Parse(parts[0]);
        var month = int.Parse(parts[1]);
        var start = new DateTime(year, month, 1);
        var end = start.AddMonths(1).AddDays(-1);
        return (start, end);
    }
}
