using System.Text.Json;
using OctopusOA.Api.DTOs;
using OctopusOA.Api.Persistence;

namespace OctopusOA.Api.Services;

/// <summary>会议室服务：房间 CRUD + 预订 + 冲突检测</summary>
public class MeetingService
{
    private readonly OaDbContext _db;

    public MeetingService(OaDbContext db) => _db = db;

    // ── 会议室 ──

    public List<MeetingRoomDto> GetRoomList(int? status = null)
    {
        var query = _db.MeetingRooms.AsQueryable();
        if (status.HasValue) query = query.Where(r => r.Status == status.Value);
        return query.OrderBy(r => r.Id).ToList().Select(MapRoom).ToList();
    }

    public MeetingRoomDto? GetRoom(long id)
    {
        var r = _db.MeetingRooms.FirstOrDefault(r => r.Id == id);
        return r == null ? null : MapRoom(r);
    }

    public MeetingRoomDto CreateRoom(CreateMeetingRoomRequest req)
    {
        var room = new OaMeetingRoom
        {
            Name = req.Name, Capacity = req.Capacity,
            Location = req.Location,
            Equipment = JsonSerializer.Serialize(req.Equipment ?? new List<string>()),
            Description = req.Description, ImageUrl = req.ImageUrl,
            Status = req.Status, CreateTime = DateTime.UtcNow,
        };
        _db.MeetingRooms.Add(room);
        _db.SaveChanges();
        return MapRoom(room);
    }

    public string? UpdateRoom(UpdateMeetingRoomRequest req)
    {
        var room = _db.MeetingRooms.FirstOrDefault(r => r.Id == req.Id);
        if (room == null) return "会议室不存在";
        room.Name = req.Name;
        room.Capacity = req.Capacity;
        room.Location = req.Location;
        room.Equipment = JsonSerializer.Serialize(req.Equipment ?? new List<string>());
        room.Description = req.Description;
        room.ImageUrl = req.ImageUrl;
        room.Status = req.Status;
        _db.SaveChanges();
        return null;
    }

    public string? DeleteRoom(long id)
    {
        var room = _db.MeetingRooms.FirstOrDefault(r => r.Id == id);
        if (room == null) return "会议室不存在";
        var nowUtc = DateTime.UtcNow;
        var hasFuture = _db.MeetingBookings.Any(b =>
            b.RoomId == id && b.Status == "confirmed" && b.EndTime > nowUtc);
        if (hasFuture) return "该会议室有未开始的预订，不可删除";
        _db.MeetingRooms.Remove(room);
        _db.SaveChanges();
        return null;
    }

    // ── 预订 ──

    public (MeetingBookingDto? booking, string? error, int code) Book(CreateBookingRequest req, long userId, string userName)
    {
        var room = _db.MeetingRooms.FirstOrDefault(r => r.Id == req.RoomId);
        if (room == null) return (null, "会议室不存在", 404);
        if (room.Status != 1) return (null, "会议室已停用", 400);
        if (req.EndTime <= req.StartTime) return (null, "结束时间必须晚于开始时间", 400);
        if (req.StartTime < DateTime.UtcNow.AddMinutes(-1)) return (null, "开始时间不能早于当前时间", 400);

        var conflict = _db.MeetingBookings.Any(b =>
            b.RoomId == req.RoomId &&
            b.Status == "confirmed" &&
            req.StartTime < b.EndTime && req.EndTime > b.StartTime);
        if (conflict) return (null, "该时段已被预订，请选择其他时间", 409);

        var booking = new OaMeetingBooking
        {
            RoomId = req.RoomId, Title = req.Title,
            UmcUserId = userId, UserName = userName,
            StartTime = req.StartTime, EndTime = req.EndTime,
            Description = req.Description,
            Attendees = JsonSerializer.Serialize(req.Attendees ?? new List<long>()),
            Status = "confirmed", CreateTime = DateTime.UtcNow,
        };
        _db.MeetingBookings.Add(booking);
        _db.SaveChanges();
        return (MapBooking(booking), null, 200);
    }

    public (string? error, int code) Cancel(long bookingId, long userId)
    {
        var b = _db.MeetingBookings.FirstOrDefault(x => x.Id == bookingId);
        if (b == null) return ("预订不存在", 404);
        if (b.UmcUserId != userId) return ("只有预订人可以取消", 403);
        if (b.Status != "confirmed") return ("当前状态不可取消", 400);
        if (b.StartTime <= DateTime.UtcNow) return ("已开始的预订不可取消", 400);
        b.Status = "cancelled";
        _db.SaveChanges();
        return (null, 200);
    }

    public List<MeetingBookingDto> GetMine(long userId)
    {
        return _db.MeetingBookings
            .Where(b => b.UmcUserId == userId)
            .OrderByDescending(b => b.StartTime)
            .ToList()
            .Select(MapBooking)
            .ToList();
    }

    public List<MeetingBookingDto> GetToday(long userId)
    {
        var todayBeijing = DateTime.UtcNow.AddHours(8).Date;
        var startUtc = todayBeijing.AddHours(-8);
        var endUtc = todayBeijing.AddDays(1).AddHours(-8);

        return _db.MeetingBookings
            .Where(b => b.Status == "confirmed" && b.StartTime < endUtc && b.EndTime > startUtc)
            .ToList()
            .Where(b => b.UmcUserId == userId || GetAttendeeIds(b).Contains(userId))
            .OrderBy(b => b.StartTime)
            .Select(MapBooking)
            .ToList();
    }

    public List<MeetingBookingDto> GetRoomCalendar(long roomId, DateTime startUtc, DateTime endUtc)
    {
        return _db.MeetingBookings
            .Where(b => b.RoomId == roomId && b.Status == "confirmed"
                     && b.StartTime < endUtc && b.EndTime > startUtc)
            .OrderBy(b => b.StartTime)
            .ToList()
            .Select(MapBooking)
            .ToList();
    }

    public List<MeetingBookingDto> GetAllCalendar(DateTime startUtc, DateTime endUtc)
    {
        return _db.MeetingBookings
            .Where(b => b.Status == "confirmed" && b.StartTime < endUtc && b.EndTime > startUtc)
            .OrderBy(b => b.StartTime)
            .ToList()
            .Select(MapBooking)
            .ToList();
    }

    // ── 辅助 ──

    private MeetingRoomDto MapRoom(OaMeetingRoom r) => new()
    {
        Id = r.Id, Name = r.Name, Capacity = r.Capacity,
        Location = r.Location,
        Equipment = ParseList(r.Equipment),
        Description = r.Description, ImageUrl = r.ImageUrl,
        Status = r.Status, CreateTime = r.CreateTime,
    };

    private MeetingBookingDto MapBooking(OaMeetingBooking b)
    {
        var room = _db.MeetingRooms.FirstOrDefault(r => r.Id == b.RoomId);
        var attendeeIds = GetAttendeeIds(b);
        var attendeeNames = attendeeIds
            .Select(id => _db.SyncUsers.FirstOrDefault(u => u.UmcUserId == id)?.NickName ?? $"用户{id}")
            .ToList();

        return new MeetingBookingDto
        {
            Id = b.Id, RoomId = b.RoomId,
            RoomName = room?.Name ?? "未知会议室",
            Title = b.Title,
            UmcUserId = b.UmcUserId, UserName = b.UserName,
            StartTime = b.StartTime, EndTime = b.EndTime,
            Description = b.Description,
            Attendees = attendeeIds,
            AttendeeNames = attendeeNames,
            Status = b.Status, CreateTime = b.CreateTime,
        };
    }

    private static List<long> GetAttendeeIds(OaMeetingBooking b)
    {
        try { return JsonSerializer.Deserialize<List<long>>(b.Attendees) ?? new(); }
        catch { return new(); }
    }

    private static List<string> ParseList(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? new(); }
        catch { return new(); }
    }
}
